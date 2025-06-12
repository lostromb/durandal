using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Durandal.Common.Config;

namespace Durandal
{
    using System.Windows;
    using System.Windows.Controls;
    
    /// <summary>
    /// Provides factory methods to convert API Configuration values into mapped Gui controls
    /// </summary>
    public class ConfigurationControlFactory
    {
        public static UIElement CreateControl(RawConfigValue value)
        {
            if (!IsGuiControl(value))
            {
                return null;
            }
            switch (value.ValueType)
            {
                case ConfigValueType.Bool:
                    return CreateBoolControl(value);
                case ConfigValueType.StringList:
                case ConfigValueType.Float:
                case ConfigValueType.Int:
                case ConfigValueType.String:
                case ConfigValueType.TimeSpan:
                    return CreateStringControl(value);
                case ConfigValueType.Binary:
                default:
                    return null;
            }
        }

        private static bool IsGuiControl(RawConfigValue value)
        {
            foreach (var x in value.Annotations)
            {
                if (x.GetTypeName().Equals("GUI"))
                {
                    return true;
                }
            }
            return false;
        }

        private static string ExtractDescription(RawConfigValue value)
        {
            foreach (var x in value.Annotations)
            {
                if (x.GetTypeName().Equals("Description"))
                {
                    return x.GetStringValue();
                }
            }
            return null;
        }

        private static UIElement CreateBoolControl(RawConfigValue value)
        {
            CheckBox returnVal = new CheckBox
            {
                Content = value.Name,
                IsChecked = bool.Parse(value.DefaultValue)
            };
            string desc = ExtractDescription(value);
            if (!string.IsNullOrEmpty(desc))
            {
                returnVal.ToolTip = desc;
            }
            return returnVal;
        }

        private static UIElement CreateStringControl(RawConfigValue value)
        {
            Grid container = new Grid();
            Label label = new Label()
            {
                Content = value.Name,
                Width = 200,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            TextBox textBox = new TextBox()
            {
                Text = value.DefaultValue,
                Margin = new Thickness(200, 0, 0, 0)
            };
            string desc = ExtractDescription(value);
            if (!string.IsNullOrEmpty(desc))
            {
                label.ToolTip = desc;
                textBox.ToolTip = desc;
            }
            container.Children.Add(label);
            container.Children.Add(textBox);
            return container;
        }
    }
}
