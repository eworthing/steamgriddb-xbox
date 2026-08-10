using System.Threading.Tasks;

using Windows.UI.ViewManagement.Core;
using Windows.UI.Xaml;

namespace SteamGridDB.Xbox.Controls
{
    /// <summary>
    /// The on-screen keyboard, for the search box.
    ///
    /// A Game Bar widget is driven by a controller as often as by a mouse, and a controller has no
    /// way to type. Both calls are wrapped because <see cref="CoreInputView"/> is not guaranteed to
    /// exist for a view - a widget hosted somewhere without one should carry on rather than throw
    /// out of a focus handler, which is an <c>async void</c> path where a throw reaches App's
    /// last-resort handler.
    /// </summary>
    internal static class VirtualKeyboard
    {
        /// <summary>
        /// Shows the keyboard, but only for focus that arrived without a pointer.
        /// </summary>
        /// <param name="focusState">How the control being focused was reached.
        /// <see cref="FocusState.Keyboard"/> is keyboard or gamepad navigation and is the case worth
        /// showing a keyboard for; <see cref="FocusState.Pointer"/> is a mouse click or a tap, where
        /// the user already has something to type with and an on-screen keyboard is in the way.</param>
        internal static async Task ShowForAsync(FocusState focusState)
        {
            if (focusState != FocusState.Keyboard)
            {
                return;
            }

            // Delay showing the keyboard to prevent Game Bar from hiding on first focus
            await Task.Delay(100);

            try
            {
                CoreInputView.GetForCurrentView().TryShow((CoreInputViewKind)7); // 7 = keyboard gamepad
            }
            catch
            {
                // Keyboard input view not available or failed to show
            }
        }

        /// <summary>
        /// Hides the keyboard, for when the box it was shown for loses focus.
        /// </summary>
        internal static void Hide()
        {
            try
            {
                CoreInputView.GetForCurrentView().TryHide();
            }
            catch
            {
                // Keyboard input view not available or failed to hide
            }
        }
    }
}
