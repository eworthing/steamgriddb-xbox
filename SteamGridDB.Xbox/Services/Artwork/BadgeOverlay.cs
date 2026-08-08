using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace SteamGridDB.Xbox.Services.Artwork
{
    /// <summary>
    /// Recognises artwork carrying a storefront badge burned into a corner - a Steam roundel on a
    /// coloured tab, laid over what is otherwise the game's real cover.
    ///
    /// This exists because the notes cannot catch these. <see cref="ArtworkRanker"/>'s console-badge
    /// vocabulary works when an uploader says what they did ("Playstation 4", "Xbox One"), and the
    /// uploads this catches say nothing at all - every notes field is empty. Nor can the
    /// official-artwork gate: a badged cover *is* the real cover, so it scores ~0.9 against Valve's
    /// capsule and the gate approves it. The badge is only visible in pixels.
    ///
    /// Deliberately an exact-overlay test, not a "does this look like a logo" test, and one entry per
    /// *rendering* rather than per badge design. Both of those were learned the hard way:
    ///
    ///   - Contrast-normalised template correlation recalled 37% and scored a known-badged cover
    ///     0.57 - a miss. The badge drowns in whatever art is behind it.
    ///   - "Corner is a flat colour absent from the middle" did not separate at all. Most cover art
    ///     has a flat corner, so clean artwork scored *higher* than badged artwork.
    ///   - Averaging one badge design across the uploaders who use it destroys it. The dark tab is
    ///     drawn at slightly different scales by different people, and a reference fitted across
    ///     them matched any dark corner - one such fit claimed 226 of 861 candidates. Fitted per
    ///     rendering instead, the same images separate by a factor of thirty.
    ///
    /// What makes a single rendering work is that it is composited, not blended: bit-for-bit
    /// identical wherever it is used, whatever is underneath. That is the whole discriminating
    /// signal, and it is why <see cref="badgeDistanceLimit"/> sits in open space rather than on a
    /// boundary.
    ///
    /// Nineteen renderings are described, covering the Steam tab in four renderings, the "STEAM"
    /// case spine, the PlayStation 4/5 and Xbox One/Series/360 spines, a PC spine in two renderings,
    /// and the Epic, Ubisoft, LEGO, Play Store and Nintendo Switch badges. Together they flag 540 of
    /// 4741 candidates, every sampled one confirmed badged by eye, with each rendering's worst
    /// flagged image well below the limit and its nearest unflagged one well above:
    ///
    ///   worst flagged across all renderings   7.9
    ///   nearest unflagged, smallest margin   15.0
    ///
    /// That corpus is deliberately not this developer's library. It is 1000 of the most-owned Steam
    /// titles plus a broad autocomplete sweep, 5.5x the size of the library the first version was
    /// measured on - because "no false positives" measured only against the games one person has
    /// installed is not a claim worth much.
    ///
    /// Adding a rendering means measuring one, not tuning this: grow the group from a reported
    /// upload, confirm the members by eye, average them, and keep the pixels whose spread across
    /// different games is lowest. A group whose members do not then sit far below the limit is not
    /// one rendering, and must be split rather than admitted.
    ///
    /// Two candidate renderings were measured and rejected rather than admitted at a lower bar: a
    /// second Epic variant, which flagged 215 candidates with no margin at all, and the "PlayStation
    /// Hits" banner, whose two renderings left margins of 6.9 and 1.5. A badge nobody has complained
    /// about is not worth a rule that might drop good artwork.
    /// </summary>
    internal static class BadgeOverlay
    {
        // The whole image is scaled to this, and the badge occupies the top-left corner of it. One
        // scale of the whole image rather than a crop transform, so this uses the decode path
        // TileImage already has.
        internal const uint ScaledSize = 64;

        // The top-left block of that scaled image the reference describes - the quarter the tab sits in.
        internal const int CornerSize = 16;

        // Mean per-channel difference from the nearest rendering, over that rendering's pixels only.
        // Across all nineteen, the worst badged artwork measures 7.9 and the nearest unflagged
        // artwork measures 15.0, so this sits inside a gap rather than on a boundary - the lesson
        // ARTWORK-SELECTION.md records from the official-artwork gate's thresholds, which did sit on
        // one and had to be widened twice. Most renderings are far tighter than that pair; 15.0 is
        // the single smallest margin any of them leaves.
        private const double badgeDistanceLimit = 10.0;

        /// <summary>
        /// One entry per known rendering, each packed as
        /// index &lt;&lt; 24 | r &lt;&lt; 16 | g &lt;&lt; 8 | b over a <see cref="CornerSize"/> square.
        ///
        /// Only the pixels a rendering covers on every upload that carries it are listed. They were
        /// not chosen by hand: the group's corners were averaged and the per-pixel spread across
        /// *different games* measured, so a pixel the overlay owns is constant and a pixel showing
        /// artwork past it is not. The low-spread pixels are the badge. A mask drawn by eye around a
        /// logo would have included pixels that merely look like part of it.
        /// </summary>
        private static readonly uint[][] renderings =
        {
            // SteamTabLight - 90 px, flags 196, worst 3.3, nearest 29.8
            new uint[]
            {
                0x041A81B0, 0x051680B0, 0x061780B0, 0x071780B0, 0x081780B0, 0x091780B0,
                0x0A1780B0, 0x13147DAF, 0x14147EB0, 0x15127DAE, 0x16137EAF, 0x171780B0,
                0x181780B0, 0x191780B0, 0x1A177FB0, 0x22167FB0, 0x23127CAE, 0x242586B2,
                0x254597BD, 0x26338EB7, 0x27137CAE, 0x281680B0, 0x291780B0, 0x31147DAF,
                0x32117CAE, 0x334E9DC1, 0x34DAEBF1, 0x35F8FBFC, 0x36CBE2EB, 0x376FAFCB,
                0x38137DAD, 0x391780B1, 0x401A81B0, 0x41157EB0, 0x422284B1, 0x43E0EEF3,
                0x44FFFFFF, 0x45B5D5E3, 0x464A9ABC, 0x484698BD, 0x49127DAF, 0x501680B0,
                0x51157FB0, 0x522486B3, 0x539FCADC, 0x54D2E7EF, 0x554294BA, 0x564F9DBF,
                0x57C6DFE9, 0x5871B0CD, 0x590E7BAD, 0x601780B0, 0x611680B0, 0x621D82B1,
                0x635EA5C5, 0x64378FB6, 0x656EAEC9, 0x66E4F0F5, 0x67FFFFFF, 0x684C9CBF,
                0x69117CAE, 0x701780B0, 0x711780B0, 0x72127DAE, 0x736FB0CC, 0x74BEDBE6,
                0x75EFF6F7, 0x76FFFFFF, 0x7797C5D9, 0x78167EAE, 0x79157FB1, 0x801780B0,
                0x811780B0, 0x821780B0, 0x83127CAE, 0x844296BC, 0x856CADCA, 0x864D9CBF,
                0x87167EAE, 0x88167FB0, 0x901780B0, 0x911780B0, 0x921780B1, 0x93177FB1,
                0x94127DAF, 0x950F7BAE, 0x96117CAE, 0x97147FB1, 0xA01780B0, 0xA11780B0,
            },
            // FramedPlate - 109 px, flags 92, worst 0.0, nearest 19.0
            new uint[]
            {
                0x02444444, 0x03444444, 0x04424242, 0x07616060, 0x08656464, 0x09646363,
                0x0C636262, 0x10565758, 0x1338393B, 0x14353638, 0x17949090, 0x1A827A79,
                0x1C79706F, 0x2238393B, 0x23363739, 0x295D4E4B, 0x2C5C4D49, 0x2E4A3631,
                0x2F584643, 0x30505052, 0x32363739, 0x38726967, 0x394F3C39, 0x3CD6D1D0,
                0x3DB7B0AE, 0x3F422E28, 0x4048494A, 0x41353638, 0x45363739, 0x50414142,
                0x51353638, 0x52353638, 0x56585A5C, 0x587D7674, 0x59615451, 0x5C5C4C47,
                0x5D6F625F, 0x5F685955, 0x61363739, 0x66595A5C, 0x69544642, 0x6A5E4E4A,
                0x6C897E7B, 0x7237383A, 0x73363739, 0x784D3F3C, 0x794F4843, 0x7A645B56,
                0x7E70625E, 0x7F422F2A, 0x803C3D3E, 0x83363739, 0x85363839, 0x87625958,
                0x88422F2C, 0x8B564C47, 0x8C6D635F, 0x8D584C47, 0x8E43322D, 0x8F544642,
                0x93363739, 0x95353739, 0x97594E4D, 0x983A2522, 0x9A4E433F, 0x9D4B3E39,
                0x9E4E403B, 0x9F4E413C, 0xA3363739, 0xA8382320, 0xB03C3D3F, 0xB237383A,
                0xB3363739, 0xB65A5C5E, 0xB759504F, 0xB8382523, 0xBA3A2A27, 0xBB3A2C29,
                0xBC3A2925, 0xC237383A, 0xC3363739, 0xC6595B5D, 0xC8392725, 0xC93A2926,
                0xCB3A2D29, 0xCC3A2B27, 0xCE3A2A26, 0xD1363739, 0xD2363739, 0xD4363739,
                0xD537383A, 0xD6595B5D, 0xD83A2B2A, 0xD93A2A28, 0xDC3A2B28, 0xDD3A2B28,
                0xDE3A2A26, 0xDF3A2824, 0xE1363739, 0xE3363739, 0xE4363739, 0xE6595B5D,
                0xE9392A29, 0xEA352523, 0xEB352623, 0xED352522, 0xF6595B5C, 0xF8372A29,
                0xFA645A59,
            },
            // LeftSpine1 - 90 px, flags 86, worst 7.4, nearest 22.1
            new uint[]
            {
                0x002378D5, 0x012378D5, 0x022378D5, 0x032378D5, 0x042476D5, 0x102274CD,
                0x112274CD, 0x122274CD, 0x132274CD, 0x142372CC, 0x20216EC2, 0x21216EC2,
                0x22216EC2, 0x23216EC2, 0x24216CC1, 0x25168ED9, 0x301F69B9, 0x311F69B9,
                0x321F69B9, 0x331F69B9, 0x341F67B7, 0x35168BD5, 0x401E63AF, 0x411E63AF,
                0x421E63AF, 0x431E63AF, 0x441E61AD, 0x451588CF, 0x501C5EA6, 0x511B5EA6,
                0x521C5EA6, 0x531B5EA6, 0x541C5BA4, 0x551485CA, 0x601B589D, 0x611B589D,
                0x621B589D, 0x631B589D, 0x641B559A, 0x651382C5, 0x70185394, 0x71185394,
                0x72185394, 0x73185394, 0x74185091, 0x751280C0, 0x80174E8A, 0x81174E8A,
                0x82174D8A, 0x83174E8A, 0x84174A87, 0x85127CBB, 0x90154980, 0x91164880,
                0x92164880, 0x93154980, 0x9416457C, 0x95107AB6, 0xA0144376, 0xA1144376,
                0xA2144376, 0xA3144376, 0xA4143F72, 0xB0133E6D, 0xB1133E6D, 0xB2133D6D,
                0xB3133E6D, 0xB4133969, 0xC0113864, 0xC1113864, 0xC2113864, 0xC3113864,
                0xC410345F, 0xC50F70A6, 0xD00F335A, 0xD10F335A, 0xD20F335A, 0xD30F335A,
                0xD40F2E55, 0xD50E6EA1, 0xE00D2D51, 0xE10D2D51, 0xE20D2D51, 0xE30D2D51,
                0xE40C294C, 0xF00C2847, 0xF10C2847, 0xF20C2847, 0xF30C2847, 0xF40C2341,
            },
            // LeftSpine3 - 90 px, flags 86, worst 7.9, nearest 23.2
            new uint[]
            {
                0x002378D5, 0x012378D5, 0x022378D5, 0x032378D5, 0x042476D4, 0x051299EE,
                0x102274CD, 0x112274CD, 0x122274CD, 0x132274CD, 0x142372CB, 0x151296EA,
                0x20216EC2, 0x21216EC2, 0x22216EC2, 0x23216EC2, 0x24226CC0, 0x251194E4,
                0x301F69B9, 0x311F69B9, 0x321F69B9, 0x331F69B9, 0x341F66B6, 0x351090DF,
                0x401E63AF, 0x411E63AF, 0x421E63AF, 0x431E63AF, 0x441E60AC, 0x450F8EDA,
                0x501C5EA6, 0x511B5EA6, 0x521C5EA6, 0x531C5EA6, 0x541C5BA3, 0x601B589D,
                0x611B589D, 0x621B589D, 0x631B589D, 0x641B559A, 0x70185394, 0x71185394,
                0x72185394, 0x73185394, 0x74184F90, 0x80174E8A, 0x81174E8A, 0x82174D8A,
                0x83174E8A, 0x84174A86, 0x850C82C6, 0x90154980, 0x91164880, 0x92164880,
                0x93154980, 0x9416447B, 0xA0144376, 0xA1144376, 0xA2144376, 0xA3144376,
                0xA4153F71, 0xA50A7CBB, 0xB0133E6D, 0xB1133E6D, 0xB2133D6D, 0xB3133E6D,
                0xB4133968, 0xB50A78B6, 0xC0113864, 0xC1113864, 0xC2113864, 0xC3113864,
                0xC411335E, 0xD00F335A, 0xD10F335A, 0xD20F335A, 0xD30F335A, 0xD40F2E54,
                0xD50872AA, 0xE00D2D51, 0xE10D2D51, 0xE20D2D51, 0xE30D2D51, 0xE40D284B,
                0xF00C2847, 0xF10C2847, 0xF20C2847, 0xF30C2847, 0xF40C2340, 0xF5086EA0,
            },
            // Xbox360Spine - 90 px, flags 47, worst 2.1, nearest 23.1
            new uint[]
            {
                0x04DCE23E, 0x05DEE23E, 0x107CC11C, 0x117BC01C, 0x127EC11C, 0x17DFE23E,
                0x207EC21D, 0x217EC21D, 0x227EC21D, 0x237CC11C, 0x247BC11C, 0x257CC01C,
                0x307EC21D, 0x317EC21D, 0x327EC21D, 0x337EC21D, 0x347EC21D, 0x357EC21D,
                0x367CC11C, 0x4080C21C, 0x417FC21C, 0x427FC21C, 0x437EC11C, 0x447EC21D,
                0x457EC21D, 0x467EC21D, 0x477EC21C, 0x487DC21C, 0x5278C01F, 0x537FC31D,
                0x5480C31D, 0x557FC21C, 0x567EC21C, 0x577DC21D, 0x587EC21D, 0x602FA428,
                0x667BC21D, 0x6780C31C, 0x6880C31C, 0x7051B123, 0x7331A526, 0x807FC31E,
                0x817DC21E, 0x907EC21D, 0x917FC21C, 0x927FC21C, 0x9380C31C, 0x9480C31C,
                0x957CC21E, 0xA07DC21D, 0xA17EC21D, 0xA27EC21D, 0xA37EC21D, 0xA47EC21D,
                0xA57EC21C, 0xA680C31D, 0xA780C31D, 0xA87DC21E, 0xB181C31E, 0xB281C31E,
                0xB380C31D, 0xB47FC21D, 0xB57EC21D, 0xB67EC21D, 0xB77EC21D, 0xB87EC21C,
                0xC157B517, 0xC261B818, 0xC369BA19, 0xC474BD1D, 0xC57DC11E, 0xC681C41E,
                0xD0219F07, 0xD125A008, 0xD228A20A, 0xD32CA30C, 0xD432A10F, 0xD648A115,
                0xE024A008, 0xE126A109, 0xE228A30A, 0xE52EA50D, 0xE62CA10D, 0xF0229F07,
                0xF227A109, 0xF32AA30A, 0xF42CA50B, 0xF52EA60D, 0xF631A70D, 0xF734A90E,
            },
            // SteamTabDarkA - 90 px, flags 34, worst 0.8, nearest 28.3
            new uint[]
            {
                0x000C1B32, 0x010C1C33, 0x020C1D34, 0x030C1D35, 0x040C1E36, 0x050C1F37,
                0x060C2038, 0x070C2039, 0x080C213A, 0x090C223B, 0x0A0D233C, 0x100C1C33,
                0x110C1D34, 0x120C1D35, 0x130C1E36, 0x140C1F37, 0x150C1F38, 0x160C2039,
                0x170C213A, 0x180C223B, 0x190D223C, 0x1A0D233D, 0x200C1D34, 0x210C1D35,
                0x220C1E36, 0x230C1F37, 0x24051932, 0x25061B34, 0x26071C35, 0x27051B35,
                0x280C223C, 0x290D233C, 0x2A0D243E, 0x300C1D35, 0x310C1E36, 0x320C1E37,
                0x33071B34, 0x36B9BFC6, 0x37616F7F, 0x380A203A, 0x390C233D, 0x3A0C253F,
                0x400C1E36, 0x410C1F37, 0x42051932, 0x44FDFEFE, 0x45FFFFFF, 0x47A1AAB4,
                0x485F6F80, 0x49061E39, 0x4A0D2640, 0x500C1F37, 0x510C1F38, 0x52061B35,
                0x53AFB6BE, 0x54FFFFFF, 0x5632455B, 0x58B5BDC5, 0x590D253F, 0x5A0C2640,
                0x600C1F38, 0x610C2039, 0x620A2039, 0x63314358, 0x690C253F, 0x700C2039,
                0x710C213A, 0x72061C36, 0x73637181, 0x74818D9A, 0x76FDFDFD, 0x77FFFFFF,
                0x800C2139, 0x810C223B, 0x820C213B, 0x83112740, 0x84818E9B, 0x880F2942,
                0x900C223B, 0x910D223C, 0x920D233C, 0x930B233C, 0x94061E39, 0x95152D46,
                0x97051F3A, 0xA00D223C, 0xA10D233D, 0xA20D243E, 0xA30D253F, 0xA50B253F,
            },
            // SwitchRoundel - 106 px, flags 29, worst 0.1, nearest 17.1
            new uint[]
            {
                0x00000000, 0x04FF0000, 0x05FF0000, 0x06FF0000, 0x07FF0000, 0x08FF0000,
                0x09FF0000, 0x0AFF0000, 0x0BFF0000, 0x0CFF0000, 0x0DFF0000, 0x0EFF0000,
                0x0FFF0000, 0x12FF0000, 0x13FF0000, 0x14FF0000, 0x15FF0000, 0x16FF0000,
                0x17FF0000, 0x18FF0000, 0x19FF0000, 0x1AFF0000, 0x21FF0000, 0x22FF0000,
                0x23FF1818, 0x24FF3838, 0x25FF1E1E, 0x26FF2C2C, 0x27FF2222, 0x28FF0000,
                0x29FF0000, 0x31FF0000, 0x32FF1A1A, 0x33FF7F7F, 0x34FF6666, 0x35FF7575,
                0x36FFD3D3, 0x37FFEFEF, 0x38FF3B3B, 0x39FF0000, 0x40FF0000, 0x41FF0000,
                0x42FF3535, 0x43FF8282, 0x44FF9292, 0x45FF6868, 0x46FFD7D7, 0x47FFFFFF,
                0x48FF7070, 0x49FF0000, 0x50FF0000, 0x51FF0000, 0x52FF3A3A, 0x53FF4949,
                0x54FF1B1B, 0x55FF7373, 0x56FF9A9A, 0x57FF9090, 0x58FF7272, 0x59FF0000,
                0x60FF0000, 0x61FF0000, 0x62FF3C3C, 0x63FF4242, 0x64FF0808, 0x65FF7171,
                0x66FFB1B1, 0x67FFBFBF, 0x68FF7474, 0x69FF0000, 0x70FF0000, 0x71FF0000,
                0x72FF2323, 0x73FF7474, 0x74FF4040, 0x75FF7676, 0x76FFDDDD, 0x77FFFFFF,
                0x78FF4A4A, 0x79FF0000, 0x80FF0000, 0x81FF0000, 0x82FF0000, 0x83FF2D2D,
                0x84FF5757, 0x85FF3131, 0x86FF4C4C, 0x87FF4141, 0x88FF0101, 0x89FF0000,
                0x90FF0000, 0x91FF0000, 0x92FF0000, 0x93FF0000, 0x94FF0000, 0x95FF0000,
                0x96FF0000, 0x97FF0000, 0x98FF0000, 0xA0FF0000, 0xA5FF0000, 0xB0FF0000,
                0xC0FF0000, 0xD0FF0000, 0xE0FF0000, 0xF0FF0000,
            },
            // ConsoleSpine2 - 146 px, flags 8, worst 0.0, nearest 38.9
            new uint[]
            {
                0x00107B13, 0x01107B13, 0x02107C13, 0x030E7910, 0x040C780F, 0x050F7912,
                0x060C780F, 0x070E7910, 0x08107B13, 0x10107B13, 0x110F7B12, 0x12057408,
                0x132B892E, 0x1499C699, 0x15CBE1CC, 0x169FC9A0, 0x17338E35, 0x18057408,
                0x200F7A12, 0x21137B16, 0x226CAD6E, 0x23388F3A, 0x24328B34, 0x256CAC6E,
                0x26358E38, 0x27328C34, 0x286EAF70, 0x29177E1A, 0x3007760B, 0x3167A967,
                0x32FFFFFF, 0x33E1EFE1, 0x34258528, 0x35007101, 0x361B801E, 0x37D6E8D6,
                0x38FFFFFF, 0x400F7912, 0x41C2DDC4, 0x42FFFFFF, 0x43AED1AE, 0x440E7811,
                0x451E8120, 0x460C760F, 0x479FC9A0, 0x48FFFFFF, 0x501F8122, 0x51E4F0E4,
                0x52E7F1E7, 0x531E8021, 0x54559F56, 0x55DDECDE, 0x5661A662, 0x57167B18,
                0x58DCECDD, 0x5A248428, 0x60177D1A, 0x61DCECDB, 0x6275B177, 0x63449547,
                0x64F3F8F3, 0x65FFFFFF, 0x66F7FBF7, 0x67519D53, 0x6862A665, 0x700B770D,
                0x717FB880, 0x724A984C, 0x73E0EEDF, 0x74FFFFFF, 0x75FFFFFE, 0x76FFFFFF,
                0x77E9F3E9, 0x784A974C, 0x800F7A11, 0x81107913, 0x8282B983, 0x83FFFFFF,
                0x84FFFFFF, 0x85FFFFFF, 0x86FFFFFF, 0x87FFFFFF, 0x8895C395, 0x90107B14,
                0x910B780E, 0x92308C33, 0x93B3D4B4, 0x94F3F8F3, 0x95FDFEFD, 0x96F4F9F4,
                0x97BAD8BA, 0x983A903D, 0xA0107B13, 0xA1107B13, 0xA20C780F, 0xA30E7910,
                0xA4318B34, 0xA5439545, 0xA6338C35, 0xA70F7912, 0xA80B780E, 0xB0107B13,
                0xB1107B13, 0xB2107B13, 0xB30F7B12, 0xB40C790F, 0xB50A780D, 0xB60B790E,
                0xB70F7B12, 0xB8107B13, 0xC0107B13, 0xC1107B13, 0xC2107B13, 0xC3107B13,
                0xC4107B13, 0xC5107B13, 0xC6107B13, 0xC7107B13, 0xC8107B13, 0xD0107B13,
                0xD1107B13, 0xD2107B13, 0xD3107B13, 0xD4107B13, 0xD5107B13, 0xD6107B13,
                0xD7107B13, 0xD8107B13, 0xE0107B13, 0xE1107B13, 0xE2107B13, 0xE3107B13,
                0xE4107B13, 0xE5107B13, 0xE6107B13, 0xE7107B13, 0xE8107B13, 0xF0107B13,
                0xF1107B13, 0xF2107B13, 0xF3107B13, 0xF4107B13, 0xF5107B13, 0xF6107B13,
                0xF7107B13, 0xF8107B13,
            },
            // PlayBadge2 - 93 px, flags 8, worst 0.3, nearest 32.0
            new uint[]
            {
                0x00000000, 0x0430DE81, 0x0530DD81, 0x0630DD81, 0x0730DD81, 0x0830DD81,
                0x0930DD81, 0x0A30DD81, 0x1232E585, 0x1330DE81, 0x1430DD81, 0x1530DD81,
                0x1630DD81, 0x1730DD81, 0x1830DD81, 0x1930DD81, 0x1A30DD81, 0x2230DD81,
                0x2330DD81, 0x2430DD81, 0x2530DD81, 0x2630DD81, 0x2730DD81, 0x2830DD81,
                0x2930DE81, 0x2A30DD81, 0x3130DD81, 0x3230DD81, 0x3333DD82, 0x342DDC7F,
                0x3528DC7C, 0x362ADC7D, 0x3733DD83, 0x3830DD81, 0x3930DE81, 0x3A30DD81,
                0x4030DD81, 0x4130DD81, 0x422EDD80, 0x433ADE87, 0x446BE6A5, 0x4577E8AC,
                0x466FE7A7, 0x474BE191, 0x482CDD7F, 0x4930DD81, 0x4A30DD81, 0x5030DD81,
                0x5130DD81, 0x522BDC7E, 0x5382EAB3, 0x54EFFCF5, 0x55FFFFFF, 0x56FAFEFC,
                0x57ACF1CD, 0x5832DD82, 0x6030DD81, 0x612DDD7F, 0x6243E08D, 0x63EBFBF2,
                0x64E8FBF1, 0x65FDFFFE, 0x66EDFCF4, 0x67F4FDF8, 0x686FE7A7, 0x6A30DD81,
                0x7030DD81, 0x712FDD80, 0x7239DE86, 0x735AE49A, 0x745BE49B, 0x7559E39A,
                0x765AE49B, 0x775BE49B, 0x7843E08C, 0x8030DD81, 0x8130DD81, 0x822FDD80,
                0x832BDC7E, 0x842BDC7E, 0x852BDC7E, 0x862BDC7E, 0x872BDC7E, 0x882EDD80,
                0x9030DD81, 0x9130DD81, 0x9230DE81, 0x9330DE81, 0x9430DE81, 0x9530DE81,
                0x9630DE81, 0xA030DD81, 0xA530DD81,
            },
            // ConsoleSpine5 - 92 px, flags 7, worst 0.3, nearest 15.0
            new uint[]
            {
                0x002F87C6, 0x012F87C7, 0x022F87C7, 0x032F87C7, 0x042F87C8, 0x052C86C9,
                0x102F87C7, 0x112F87C7, 0x122C85C6, 0x132883C6, 0x142D86C7, 0x152C87CA,
                0x202F87C7, 0x212C84C5, 0x2262A4D3, 0x237CB2D8, 0x243B8DC8, 0x252A86C9,
                0x302F86C6, 0x312580C3, 0x3292BFDF, 0x33DAE9F1, 0x34B5D5EA, 0x352B84C8,
                0x402C83C4, 0x412F83C4, 0x429BC4E1, 0x43BFD9EA, 0x44B9D7EA, 0x453186C7,
                0x506CA8D4, 0x51B0D1E4, 0x52C5DCEC, 0x53C5DCE9, 0x54AACCE0, 0x55A4CCE6,
                0x604590C9, 0x615C9ECD, 0x6278AED6, 0x6399C3DE, 0x6477AED5, 0x702C80C2,
                0x71297FC3, 0x72297EC1, 0x732B7FC2, 0x74287EC2, 0x752880C5, 0x802D80C2,
                0x812D80C2, 0x822D80C2, 0x832D80C2, 0x842D80C2, 0x902D80C2, 0x912D80C2,
                0x922D80C2, 0x932D80C2, 0x942D80C2, 0xA02D7EC0, 0xA12D7EC0, 0xA22D7EC0,
                0xA32D7EC0, 0xA42D7EC0, 0xA5287EC3, 0xB02C7DBF, 0xB12C7DBF, 0xB22C7DBF,
                0xB32C7DBF, 0xB42C7DBF, 0xB5287CC1, 0xC02B7CBE, 0xC12B7CBE, 0xC22B7CBE,
                0xC32B7CBE, 0xC42B7CBE, 0xC5277BC0, 0xD02A7ABD, 0xD12B7ABD, 0xD22B7ABD,
                0xD32A7BBD, 0xD42A7BBD, 0xE02A79BC, 0xE12A79BC, 0xE22A79BC, 0xE32979BC,
                0xE42979BC, 0xE52678BE, 0xF02977BB, 0xF12977BC, 0xF22977BC, 0xF32977BC,
                0xF42977BC, 0xF52577BE,
            },
            // UbisoftBadge - 90 px, flags 7, worst 0.0, nearest 22.2
            new uint[]
            {
                0x00000000, 0x01001620, 0x020274A8, 0x0303A7F1, 0x0403A9F5, 0x0503A9F4,
                0x0603A9F4, 0x0703A9F4, 0x0803A9F4, 0x0903A9F4, 0x0A03A9F4, 0x10001925,
                0x11028FCE, 0x1203AFFD, 0x1303A9F5, 0x1403A9F4, 0x1503A9F4, 0x1603A9F4,
                0x1703A9F4, 0x1803A9F4, 0x1903A9F4, 0x1A03A9F4, 0x20027FB8, 0x2103AFFC,
                0x2202A8F4, 0x2300A6F4, 0x2400A8F4, 0x2500A6F4, 0x2600A8F4, 0x2700A6F4,
                0x2800A8F4, 0x3003A9F4, 0x3102A9F4, 0x320CACF4, 0x3362C9F8, 0x342FB8F6,
                0x3563CAF8, 0x3630B8F6, 0x3761C9F8, 0x3824B4F5, 0x4003A9F4, 0x4100A7F4,
                0x4236BAF6, 0x43FFFFFF, 0x446ECDF8, 0x45F4FBFF, 0x4673CFF8, 0x47F3FBFE,
                0x4874CFF9, 0x5003A9F4, 0x5100A7F4, 0x5238BBF6, 0x53FFFFFF, 0x5468CBF8,
                0x55CDEEFD, 0x5660C8F7, 0x57EFF9FE, 0x5873CFF9, 0x6003A9F4, 0x6100A7F4,
                0x6238BBF6, 0x63FFFFFF, 0x64CDEEFD, 0x6565C9F8, 0x669EDEFA, 0x67FFFFFF,
                0x6873CFF9, 0x6900A6F4, 0x7003A9F4, 0x7101A8F4, 0x7215AFF5, 0x738FD9FA,
                0x74A1DFFB, 0x759BDDFB, 0x76A1DFFB, 0x779BDDFB, 0x7836BAF6, 0x8003A9F4,
                0x8103A9F4, 0x8201A8F4, 0x8300A6F4, 0x8400A6F4, 0x8500A6F4, 0x8600A6F4,
                0x8700A6F4, 0x9003A9F4, 0x9103A9F4, 0xA003A9F4, 0xA103A9F4, 0xA403A9F4,
            },
            // SteamTabDarkC - 97 px, flags 6, worst 0.1, nearest 23.6
            new uint[]
            {
                0x00000000, 0x01030407, 0x020C1420, 0x03101C2D, 0x040A1728, 0x05071426,
                0x06071528, 0x070C1A2D, 0x08122033, 0x09122134, 0x0A132135, 0x10030508,
                0x110E1927, 0x120F1C2E, 0x130E1A2C, 0x14495360, 0x157F8690, 0x167C838D,
                0x17424D5C, 0x180D1C30, 0x1A132237, 0x200D1724, 0x21101D2E, 0x22131F30,
                0x239FA4AB, 0x24FDFDFD, 0x25FFFFFF, 0x26FFFFFF, 0x27E8E9EB, 0x2889909A,
                0x2A122337, 0x30111D2F, 0x31091527, 0x327A818B, 0x33FFFFFF, 0x34FFFFFF,
                0x35FCFCFD, 0x36818892, 0x374F5A68, 0x38C1C5C9, 0x40101C2D, 0x41142032,
                0x42DADBDE, 0x43FFFFFF, 0x44FFFFFF, 0x45B3B7BE, 0x46384353, 0x471C2A3D,
                0x489399A2, 0x4A0E1F35, 0x50101D2F, 0x51142133, 0x527B838D, 0x53CBCED2,
                0x54D7D9DC, 0x55273547, 0x5619273B, 0x5779818C, 0x58E0E2E4, 0x60111F31,
                0x61111E31, 0x621A2739, 0x6319273A, 0x641C2A3D, 0x65334050, 0x66A3A9B1,
                0x67FEFEFE, 0x68FFFFFF, 0x6A0C1F36, 0x70121F32, 0x710C1B2E, 0x72515C6A,
                0x73CDD0D4, 0x7479818B, 0x75BEC2C7, 0x76FFFFFF, 0x77FFFFFF, 0x78F4F4F6,
                0x80122033, 0x81122034, 0x820F1E32, 0x83717A85, 0x84E7E9EB, 0x85FFFFFF,
                0x86FEFEFE, 0x87D7DADD, 0x88566373, 0x90122134, 0x92132236, 0x930C1C31,
                0x96465364, 0xA0132235, 0xA1132237, 0xA3132439, 0xA4112238, 0xA50D1F36,
                0xA60E2137,
            },
            // ConsoleSpinePs4Alt - 147 px, flags 5, worst 0.0, nearest 25.2
            new uint[]
            {
                0x003086C7, 0x013086C7, 0x023086C7, 0x032E85C7, 0x044793CA, 0x05418FC8,
                0x062A82C4, 0x072B83C6, 0x082F86C7, 0x102F87C7, 0x112F87C7, 0x122F87C7,
                0x132882C4, 0x14A0C9E3, 0x15E2EEF5, 0x16A4CAE3, 0x174E97CC, 0x182C84C6,
                0x202F87C7, 0x212F87C7, 0x222F87C7, 0x232882C4, 0x24A7CDE5, 0x25DFECF5,
                0x26C0D8EA, 0x27D8E9F3, 0x283388C6, 0x302F86C6, 0x312F86C6, 0x322E86C7,
                0x33237EC3, 0x34A6CBE4, 0x35D9E9F4, 0x36B1D0E6, 0x37F5FAFC, 0x383D8DC9,
                0x392A83C6, 0x403085C6, 0x412A81C4, 0x423084C4, 0x435299CC, 0x44B7D4E7,
                0x45D9E9F4, 0x468FBBDA, 0x47B2D1E4, 0x483C8BC7, 0x492981C3, 0x502C82C4,
                0x5175AED6, 0x52C4DCEB, 0x53B3D2E4, 0x54BFD9E9, 0x55E1EDF6, 0x56A3C8E1,
                0x57A1C6DE, 0x58C4DBEA, 0x602C81C3, 0x6180B4D9, 0x62B5D2E6, 0x6390BDDB,
                0x64BFDAE9, 0x65E6F0F7, 0x669EC4DD, 0x67BDD7E8, 0x68ABCCE3, 0x702D82C4,
                0x71287EC1, 0x722E81C2, 0x733284C3, 0x744C94CA, 0x757FB2D8, 0x7671AAD3,
                0x774790C7, 0x78297EC1, 0x79267DC2, 0x802D80C2, 0x812D80C2, 0x822D80C2,
                0x832D80C2, 0x842A7EC2, 0x85277CC0, 0x86277DC1, 0x872A7FC2, 0x882D80C3,
                0x902D80C2, 0x912D80C2, 0x922D80C2, 0x932D80C2, 0x942D80C2, 0x952D80C2,
                0x962D80C2, 0x972D80C2, 0x982D80C2, 0xA02D7EC0, 0xA12D7EC0, 0xA22D7EC0,
                0xA32D7EC0, 0xA42D7EC0, 0xA52D7EC0, 0xA62D7EC0, 0xA72D7EC0, 0xA82D7EC0,
                0xB02C7DBF, 0xB12C7DBF, 0xB22C7DBF, 0xB32C7DBF, 0xB42C7DBF, 0xB52C7DBF,
                0xB62C7DBF, 0xB72C7DBF, 0xB82C7DBF, 0xC02B7CBE, 0xC12B7CBE, 0xC22B7CBE,
                0xC32B7CBE, 0xC42B7CBE, 0xC52B7CBE, 0xC62B7CBE, 0xC72B7CBE, 0xC82B7CBE,
                0xD02A7ABD, 0xD12A7ABD, 0xD22A7ABD, 0xD32A7BBD, 0xD42A7ABD, 0xD52A7BBD,
                0xD62A7BBD, 0xD72A7BBD, 0xD82A7BBD, 0xE02A79BD, 0xE12A79BE, 0xE22A79BD,
                0xE32A79BC, 0xE42A79BC, 0xE52A79BC, 0xE62A79BC, 0xE72A79BC, 0xE82A79BC,
                0xF02977BB, 0xF12977BB, 0xF22977BB, 0xF32977BB, 0xF42977BC, 0xF52977BB,
                0xF62977BC, 0xF72977BB, 0xF82977BB,
            },
            // SteamTabDarkB - 90 px, flags 4, worst 6.2, nearest 22.1
            new uint[]
            {
                0x00000000, 0x01070A0D, 0x02112D43, 0x03113F61, 0x040D3F63, 0x050A3D62,
                0x060C3E63, 0x070E4165, 0x080E4165, 0x090E4165, 0x0A0E4166, 0x0B094167,
                0x0C084168, 0x0D084169, 0x0E084169, 0x0F084269, 0x10060B10, 0x11143652,
                0x120F4267, 0x13083B60, 0x14114166, 0x1616466A, 0x17083C61, 0x180C3F64,
                0x190E4166, 0x1A0E4166, 0x20103049, 0x210E4166, 0x220A3C60, 0x28124367,
                0x290B4065, 0x2A0E4266, 0x300F3F63, 0x31083B5F, 0x34FFFFFF, 0x35E8ECEF,
                0x37A5B7C5, 0x39093E63, 0x3A0F4166, 0x400D3F63, 0x41114265, 0x43FFFFFF,
                0x44FFFFFF, 0x4592A8B8, 0x4635607F, 0x48C1CFDA, 0x4A0B3F64, 0x500C3F63,
                0x51124266, 0x53DFE7EC, 0x54CDD7DE, 0x563F6885, 0x58E6ECF0, 0x5A0A3E62,
                0x600E4064, 0x610E4065, 0x62255373, 0x643A6483, 0x67F1F4F5, 0x6A0B3F63,
                0x700E4064, 0x71093D61, 0x75C6D2D9, 0x76FFFFFF, 0x77FDFDFE, 0x790D4268,
                0x7A1B4365, 0x800E4165, 0x810C3F64, 0x82134469, 0x88225274, 0x89033F67,
                0x900E4165, 0x910E4165, 0x920B4065, 0x930A3F64, 0x970D4368, 0x98033F67,
                0x99314662, 0xA00E4165, 0xA10E4166, 0xA20F4165, 0xA30F4165, 0xA40C3E63,
                0xA71E4364, 0xB00A4167, 0xC0094268, 0xD0094168, 0xE0094169, 0xF00A4169,
            },
            // ConsoleSpine4 - 158 px, flags 3, worst 0.0, nearest 39.6
            new uint[]
            {
                0x00474747, 0x01474747, 0x02474747, 0x03454545, 0x04434343, 0x05454545,
                0x06434343, 0x07454545, 0x08474747, 0x09474747, 0x10474747, 0x11464646,
                0x123E3E3E, 0x135C5C5C, 0x14B1B1B1, 0x15D7D7D7, 0x16B6B6B6, 0x17626262,
                0x183E3E3E, 0x19464646, 0x20464646, 0x21494949, 0x228E8E8E, 0x23656565,
                0x24606060, 0x258D8D8D, 0x26636363, 0x27606060, 0x28909090, 0x294C4C4C,
                0x30404040, 0x31898989, 0x32FFFFFF, 0x33E9E9E9, 0x34575757, 0x353A3A3A,
                0x364F4F4F, 0x37E0E0E0, 0x38FFFFFF, 0x39969696, 0x40454545, 0x41D1D1D1,
                0x42FFFFFF, 0x43C0C0C0, 0x44444444, 0x45515151, 0x46424242, 0x47B5B5B5,
                0x48FFFFFF, 0x49DEDEDE, 0x50515151, 0x51EBEBEB, 0x52EDEDED, 0x53515151,
                0x547B7B7B, 0x55E6E6E6, 0x56858585, 0x574A4A4A, 0x58E5E5E5, 0x604B4B4B,
                0x61E5E5E5, 0x62949494, 0x636E6E6E, 0x64F6F6F6, 0x65FFFFFF, 0x66F9F9F9,
                0x67787878, 0x68858585, 0x6A505151, 0x70424242, 0x719D9D9D, 0x72737373,
                0x73E7E7E7, 0x74FFFFFF, 0x75FFFFFF, 0x76FFFFFF, 0x77EFEFEF, 0x78727272,
                0x80464646, 0x81464646, 0x829F9F9F, 0x83FFFFFF, 0x84FFFFFF, 0x85FFFFFF,
                0x86FFFFFF, 0x87FFFFFF, 0x88ADADAD, 0x8A454646, 0x90474747, 0x91434343,
                0x925F5F5F, 0x93C4C4C4, 0x94F6F6F6, 0x95FEFEFE, 0x96F7F7F7, 0x97CACACA,
                0x98666666, 0x9A464747, 0xA0474747, 0xA1474747, 0xA2444444, 0xA3454545,
                0xA45F5F5F, 0xA56C6C6C, 0xA6616161, 0xA7464646, 0xA8434343, 0xA9474747,
                0xB0474747, 0xB1474747, 0xB2474747, 0xB3474747, 0xB4444444, 0xB5424242,
                0xB6434343, 0xB7464646, 0xB8474747, 0xB9474747, 0xBA464647, 0xC0474747,
                0xC1474747, 0xC2474747, 0xC3474747, 0xC4474747, 0xC5474747, 0xC6474747,
                0xC7474747, 0xC8474747, 0xC9474747, 0xD0474747, 0xD1474747, 0xD2474747,
                0xD3474747, 0xD4474747, 0xD5474747, 0xD6474747, 0xD7474747, 0xD8474747,
                0xE0474747, 0xE1474747, 0xE2474747, 0xE3474747, 0xE4474747, 0xE5474747,
                0xE6474747, 0xE7474747, 0xE8474747, 0xE9474747, 0xF0474747, 0xF1474747,
                0xF2474747, 0xF3474747, 0xF4474747, 0xF5474747, 0xF6474747, 0xF7474747,
                0xF8474747, 0xF9474747,
            },
            // LegoBadge4 - 92 px, flags 3, worst 0.1, nearest 21.0
            new uint[]
            {
                0x00CFB7DB, 0x01816DB5, 0x025354A6, 0x035C5CA7, 0x04615FA5, 0x056460A3,
                0x10E9DEEE, 0x11DBCDE6, 0x129589C8, 0x135D5EB3, 0x20EFC9F3, 0x21E8D3F0,
                0x22E4DDEE, 0x23B7AFDB, 0x30F4A6F8, 0x31EDB3F5, 0x32E6C5F1, 0x40F68FFA,
                0x41F396F9, 0x42EEA4F6, 0x43EBC6F4, 0x50F895FC, 0x51F891FB, 0x52F596F9,
                0x53F2ADF8, 0x60FCA4FD, 0x61FCA1FC, 0x62FA9DFB, 0x63F8A3FB, 0x70FDB2FE,
                0x71FDB1FE, 0x72FCAEFE, 0x73FBADFD, 0x80FEC0FE, 0x81FDBEFE, 0x82FDBCFD,
                0x83FDBBFE, 0x90FDD9FD, 0x91FECEFD, 0x92FEC7FE, 0x93FDC5FE, 0x94FEC4FD,
                0xA0FEFEFE, 0xA1FEF1FE, 0xA2FEDDFD, 0xA3FDCEFE, 0xA4FECCFE, 0xB0FFFFFF,
                0xB1FEFFFE, 0xB2FFFEFE, 0xB3FEF1FE, 0xB4FDDCFE, 0xB5FED3FE, 0xC0FFFFFF,
                0xC1FFFFFE, 0xC2FFFFFF, 0xC3FFFFFE, 0xC4FFFDFF, 0xC5FEF0FD, 0xD0FFFFFF,
                0xD1FFFFFF, 0xD2FFFFFF, 0xD3FFFFFF, 0xD4FFFFFF, 0xD5FFFFFF, 0xD6FEFDFE,
                0xD7FEEEFD, 0xE0FFFFFF, 0xE1FFFFFF, 0xE2FFFFFF, 0xE3FFFFFF, 0xE4FFFFFF,
                0xE5FFFFFF, 0xE6FFFFFF, 0xE7FFFFFE, 0xE8FEFCFE, 0xF0FFFFFF, 0xF1FFFFFF,
                0xF2FFFFFF, 0xF3FFFFFF, 0xF4FFFFFF, 0xF5FFFFFF, 0xF6FFFFFF, 0xF7FFFFFF,
                0xF8FFFFFF, 0xF9FFFFFF, 0xFAFEF9FE, 0xFBFEE6FE, 0xFCFDD6FE, 0xFDFCCDFD,
                0xFEFACDFD, 0xFFF8D4FE,
            },
            // ConsoleSpine3 - 159 px, flags 2, worst 0.0, nearest 33.6
            new uint[]
            {
                0x00FFFFFF, 0x01FFFFFF, 0x02FFFFFF, 0x03FFFFFF, 0x04E0E0E0, 0x05EAEAEA,
                0x06FFFFFF, 0x07FFFFFF, 0x08FFFFFF, 0x09FFFFFF, 0x10FFFFFF, 0x11FFFFFF,
                0x12FFFFFF, 0x13FFFFFF, 0x14707070, 0x15222222, 0x166B6B6B, 0x17D7D7D7,
                0x18FFFFFF, 0x19FFFFFF, 0x20FFFFFF, 0x21FFFFFF, 0x22FFFFFF, 0x23FFFFFF,
                0x24686868, 0x25252525, 0x264E4E4E, 0x272C2C2C, 0x28F8F8F8, 0x29FFFFFF,
                0x30FFFFFF, 0x31FFFFFF, 0x32FFFFFF, 0x33FFFFFF, 0x346A6A6A, 0x352D2D2D,
                0x365F5F5F, 0x370A0A0A, 0x38EFEFEF, 0x39FFFFFF, 0x40FFFFFF, 0x41FFFFFF,
                0x42FDFDFD, 0x43CECECE, 0x44565656, 0x452C2C2C, 0x46888888, 0x475B5B5B,
                0x48EDEDED, 0x49FFFFFF, 0x50FFFFFF, 0x51A5A5A5, 0x52454545, 0x535A5A5A,
                0x544C4C4C, 0x55212121, 0x566C6C6C, 0x57727272, 0x58474747, 0x60FFFFFF,
                0x61989898, 0x62585858, 0x63818181, 0x644B4B4B, 0x651B1B1B, 0x66747474,
                0x674F4F4F, 0x68646464, 0x69BCBBBB, 0x70FFFFFF, 0x71FFFFFF, 0x72FFFFFF,
                0x73F7F7F7, 0x74D8D8D8, 0x75989898, 0x76AAAAAA, 0x77E0E0E0, 0x78FFFFFF,
                0x79FFFFFF, 0x80FFFFFF, 0x81FFFFFF, 0x82FFFFFF, 0x83FFFFFF, 0x84FFFFFF,
                0x85FFFFFF, 0x86FFFFFF, 0x87FFFFFF, 0x88FFFFFF, 0x89FFFFFF, 0x90FFFFFF,
                0x91FFFFFF, 0x92FFFFFF, 0x93FFFFFF, 0x94FFFFFF, 0x95FFFFFF, 0x96FFFFFF,
                0x97FFFFFF, 0x98FFFFFF, 0x99FFFFFF, 0xA0FFFFFF, 0xA1FFFFFF, 0xA2FFFFFF,
                0xA3FFFFFF, 0xA4FFFFFF, 0xA5FFFFFF, 0xA6FFFFFF, 0xA7FFFFFF, 0xA8FFFFFF,
                0xA9FFFFFF, 0xB0FFFFFF, 0xB1FFFFFF, 0xB2FFFFFF, 0xB3FFFFFF, 0xB4FFFFFF,
                0xB5FFFFFF, 0xB6FFFFFF, 0xB7FFFFFF, 0xB8FFFFFF, 0xB9FFFFFF, 0xC0FFFFFF,
                0xC1FFFFFF, 0xC2FFFFFF, 0xC3FFFFFF, 0xC4FFFFFF, 0xC5FFFFFF, 0xC6FFFFFF,
                0xC7FFFFFF, 0xC8FFFFFF, 0xC9FFFFFF, 0xD0FFFFFF, 0xD1FFFFFF, 0xD2FFFFFF,
                0xD3FFFFFF, 0xD4FFFFFF, 0xD5FFFFFF, 0xD6FFFFFF, 0xD7FFFFFF, 0xD8FFFFFF,
                0xD9FFFFFF, 0xE0FFFFFF, 0xE1FFFFFF, 0xE2FFFFFF, 0xE3FFFFFF, 0xE4FFFFFF,
                0xE5FFFFFF, 0xE6FFFFFF, 0xE7FFFFFF, 0xE8FFFFFF, 0xE9FFFFFF, 0xF0FFFFFF,
                0xF1FFFFFF, 0xF2FFFFFF, 0xF3FFFFFF, 0xF4FFFFFF, 0xF5FFFFFF, 0xF6FFFFFF,
                0xF7FFFFFF, 0xF8FFFFFF, 0xF9FFFFFF,
            },
            // EpicRoundel2 - 91 px, flags 2, worst 0.0, nearest 22.6
            new uint[]
            {
                0x00000000, 0x01010101, 0x02010101, 0x03020202, 0x04020202, 0x05030303,
                0x06040404, 0x07040404, 0x08050505, 0x09050505, 0x0A060606, 0x10010101,
                0x11010101, 0x12020202, 0x13020202, 0x14030303, 0x15030303, 0x16040404,
                0x17040404, 0x18050505, 0x19060606, 0x20010101, 0x21020202, 0x22020202,
                0x23000000, 0x24000000, 0x25000000, 0x26000000, 0x27000000, 0x28030303,
                0x30020202, 0x31020202, 0x32010101, 0x334B4B4B, 0x34A7A7A7, 0x35A3A3A3,
                0x36A7A7A7, 0x37A7A7A7, 0x38444444, 0x39020202, 0x40030303, 0x41030303,
                0x42000000, 0x43757575, 0x44A7A7A7, 0x45797979, 0x466E6E6E, 0x47B7B7B7,
                0x486E6E6E, 0x49000000, 0x50030303, 0x51040404, 0x52000000, 0x53717171,
                0x54969696, 0x558B8B8B, 0x566D6D6D, 0x57AEAEAE, 0x586A6A6A, 0x59000000,
                0x60040404, 0x61040404, 0x62000000, 0x63757575, 0x64C3C3C3, 0x65BFBFBF,
                0x66ABABAB, 0x67C0C0C0, 0x686C6C6C, 0x69000000, 0x70040404, 0x71050505,
                0x72000000, 0x736A6A6A, 0x74E1E1E1, 0x75C5C5C5, 0x76C4C4C4, 0x77E0E0E0,
                0x79000000, 0x80050505, 0x81050505, 0x82050505, 0x830E0E0E, 0x84595959,
                0x85989898, 0x87555555, 0x880F0F0F, 0x90050505, 0x91060606, 0x94000000,
                0xA0060606,
            },
            // PlayBadge1 - 94 px, flags 2, worst 0.3, nearest 50.3
            new uint[]
            {
                0x2D3EE4FA, 0x2E3EE5FA, 0x2F3FE6FB, 0x3C3EE8FB, 0x3D3FE8FA, 0x3E42E8FA,
                0x3F46EAFA, 0x4C45EAFC, 0x4D48EBFA, 0x4E4CECFA, 0x4F52ECFA, 0x5C4EF1FE,
                0x5D50F3FF, 0x5E58F2FF, 0x5F61F0FC, 0x6E7ED5DE, 0x6F75F1FC, 0x7E98CCD0,
                0x7F86F3FC, 0x8CF23E3E, 0x8E90FCFF, 0x8F96F6FE, 0x9BD4777F, 0x9DC4F2F4,
                0x9EBEFBFE, 0x9FB7F9FE, 0xAAC7C2C4, 0xABCCCACB, 0xACC3FAFC, 0xADC6FAFC,
                0xAECAFBFE, 0xAFD0FCFE, 0xB88AF6FF, 0xB98EF7FF, 0xBA92FBFF, 0xBBA0FCFF,
                0xBCB3FAFE, 0xBDC3FBFF, 0xBED1FFFF, 0xBFDCFFFF, 0xC384F5FE, 0xC488F8FF,
                0xC58EF2FD, 0xC792F2FA, 0xC899FAFF, 0xC99CF7FE, 0xCAA0F7FE, 0xCBA4F8FE,
                0xCCAFFCFF, 0xCDB8FAFC, 0xCEAADAC5, 0xCF93B68D, 0xD28AF6FF, 0xD38FFAFF,
                0xD490DAE9, 0xD58094AC, 0xD6828AA4, 0xD77C95B4, 0xD898DFF2, 0xD9AAFEFF,
                0xDAADFBFF, 0xDBB4FCFF, 0xDCA2DBBC, 0xDD6B9446, 0xDE44660A, 0xDF3E5F00,
                0xE297FBFF, 0xE399E3F2, 0xE469708B, 0xE52C2B36, 0xE63E414E, 0xE7767998,
                0xE86A7AAF, 0xE9A2D5EE, 0xEAB0E8F6, 0xEBA0D3B4, 0xEC5E8012, 0xED436400,
                0xEE426407, 0xEF3C5A0A, 0xF2A2F8FE, 0xF3748DA7, 0xF4262632, 0xF51A1B22,
                0xF622232B, 0xF760657C, 0xF84D5177, 0xF9606499, 0xFA7A82B8, 0xFB6F8C36,
                0xFC567808, 0xFD3C5A14, 0xFE32432E, 0xFF454F4B,
            },
        };

        /// <summary>
        /// The renderings, for tests that need to paint artwork carrying one. Packed as
        /// <see cref="renderings"/> describes.
        ///
        /// A test that paints from this table and then asserts the check finds it is not proving the
        /// reference is right - only the app's own library measurements can do that, and they are
        /// recorded on this class. What it does pin is the part that can silently be wrong: the
        /// unpacking and the row stride, where the corner is indexed at <see cref="CornerSize"/> but
        /// addressed in a buffer <see cref="ScaledSize"/> wide.
        /// </summary>
        internal static IReadOnlyList<uint[]> Renderings => renderings;

        /// <summary>
        /// Whether this artwork carries a storefront badge burned into its corner.
        ///
        /// Undecodable artwork returns false. A tile that cannot be read is the download's problem to
        /// report, and answering "yes, badged" for it would quietly drop a candidate for a reason
        /// that has nothing to do with badges.
        /// </summary>
        /// <param name="imageBytes">Encoded artwork.</param>
        internal static async Task<bool> CarriesBadgeAsync(IBuffer imageBytes)
        {
            return await TileImage.WithDecoderAsync(
                imageBytes,
                async decoder => BadgeDistance(
                    await TileImage.ScaledPixelsAsync(
                        decoder, null, ScaledSize, ScaledSize, BitmapAlphaMode.Ignore))
                    <= badgeDistanceLimit,
                false,
                "Error inspecting artwork for a storefront badge");
        }

        /// <summary>
        /// How far this artwork's corner is from the nearest known rendering - the mean per-channel
        /// difference, over that rendering's own pixels. Exposed for the tests, which need to see the
        /// margin rather than just which side of it a given image fell.
        ///
        /// Nearest rather than any-within-the-limit because the renderings do not compete: an image
        /// carrying one is far from all the others, so the minimum is the only one that can be small.
        /// </summary>
        /// <param name="bgraPixels">BGRA pixels of the whole image at <see cref="ScaledSize"/> square.</param>
        internal static double BadgeDistance(byte[] bgraPixels)
        {
            if (bgraPixels == null || bgraPixels.Length < ScaledSize * ScaledSize * 4)
            {
                return double.MaxValue;
            }

            double nearest = double.MaxValue;

            foreach (uint[] rendering in renderings)
            {
                nearest = Math.Min(nearest, DistanceTo(bgraPixels, rendering));
            }

            return nearest;
        }

        /// <summary>
        /// Mean per-channel difference between the artwork's corner and one rendering.
        /// </summary>
        /// <param name="bgraPixels">BGRA pixels of the whole image at <see cref="ScaledSize"/> square.</param>
        /// <param name="rendering">One entry of <see cref="renderings"/>.</param>
        private static double DistanceTo(byte[] bgraPixels, uint[] rendering)
        {
            double total = 0;

            foreach (uint packed in rendering)
            {
                int index = (int)(packed >> 24);
                int x = index % CornerSize;
                int y = index / CornerSize;

                // BGRA, and the corner is the top-left of the full scaled image, so the row stride is
                // the scaled width rather than the corner's
                int offset = (((y * (int)ScaledSize) + x) * 4);

                int difference = Math.Abs(bgraPixels[offset + 2] - (int)((packed >> 16) & 0xFF))
                    + Math.Abs(bgraPixels[offset + 1] - (int)((packed >> 8) & 0xFF))
                    + Math.Abs(bgraPixels[offset] - (int)(packed & 0xFF));

                total += difference / 3.0;
            }

            return total / rendering.Length;
        }
    }
}
