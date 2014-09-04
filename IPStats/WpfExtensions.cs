using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace SoftFluent.Tools
{
    public static class WpfExtensions
    {
        public static void RaiseMenuItemClickOnKeyGesture(this ItemsControl control, KeyEventArgs args)
        {
            RaiseMenuItemClickOnKeyGesture(control, args, true);
        }

        public static void RaiseMenuItemClickOnKeyGesture(this ItemsControl control, KeyEventArgs args, bool throwOnError)
        {
            if (args == null)
                throw new ArgumentNullException("e");

            if (control == null)
                return;

            KeyGestureConverter kgc = new KeyGestureConverter();
            foreach (var item in control.Items.OfType<MenuItem>())
            {
                if (!string.IsNullOrWhiteSpace(item.InputGestureText))
                {
                    KeyGesture gesture = null;
                    if (throwOnError)
                    {
                        gesture = kgc.ConvertFrom(item.InputGestureText) as KeyGesture;
                    }
                    else
                    {
                        try
                        {
                            gesture = kgc.ConvertFrom(item.InputGestureText) as KeyGesture;
                        }
                        catch
                        {
                        }
                    }

                    if (gesture != null && gesture.Matches(null, args))
                    {
                        //System.Diagnostics.Trace.WriteLine("MATCH item:" + item + ", key:" + e.Key + " Keyboard.Modifiers:" + Keyboard.Modifiers);
                        item.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                        args.Handled = true;
                        return;
                    }
                }

                RaiseMenuItemClickOnKeyGesture(item, args, throwOnError);
                if (args.Handled)
                    return;
            }
        }
    }
}
