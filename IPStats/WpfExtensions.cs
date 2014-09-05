using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SoftFluent.Tools
{
    public static class WpfExtensions
    {
        public static IEnumerable<DependencyObject> EnumerableVisualChildren(this DependencyObject obj)
        {
            return obj.EnumerableVisualChildren(true);
        }

        public static IEnumerable<DependencyObject> EnumerableVisualChildren(this DependencyObject obj, bool recursive)
        {
            return obj.EnumerableVisualChildren(recursive, true);
        }

        public static IEnumerable<DependencyObject> EnumerableVisualChildren(this DependencyObject obj, bool recursive, bool sameLevelFirst)
        {
            if (obj == null)
                yield break;

            if (sameLevelFirst)
            {
                int count = VisualTreeHelper.GetChildrenCount(obj);
                List<DependencyObject> list = new List<DependencyObject>(count);
                for (int i = 0; i < count; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child == null)
                        continue;

                    yield return child;
                    if (recursive)
                    {
                        list.Add(child);
                    }
                }

                foreach (var child in list)
                {
                    foreach (DependencyObject grandChild in child.EnumerableVisualChildren(recursive, sameLevelFirst))
                    {
                        yield return grandChild;
                    }
                }
            }
            else
            {
                int count = VisualTreeHelper.GetChildrenCount(obj);
                for (int i = 0; i < count; i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(obj, i);
                    if (child == null)
                        continue;

                    yield return child;
                    if (recursive)
                    {
                        foreach (var dp in child.EnumerableVisualChildren(recursive, sameLevelFirst))
                        {
                            yield return dp;
                        }
                    }
                }
            }
        }

        public static T FindVisualChild<T>(this DependencyObject obj, string name) where T : FrameworkElement
        {
            foreach (T item in obj.EnumerableVisualChildren(true, true).OfType<T>())
            {
                if (item.Name == name)
                    return item;
            }
            return null;
        }

        public static T GetVisualParent<T>(this DependencyObject obj) where T : DependencyObject
        {
            if (obj == null)
                return null;

            DependencyObject parent = VisualTreeHelper.GetParent(obj);
            if (parent == null)
                return null;

            if (typeof(T).IsAssignableFrom(parent.GetType()))
                return (T)parent;

            return parent.GetVisualParent<T>();
        }

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
