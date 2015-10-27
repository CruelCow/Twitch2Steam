using log4net.Layout;
using System;
using System.Reflection;

namespace Twitch2Steam
{
    /// <summary>
    /// Normally the patternlayout is relatively limited to what values Header/Footer can be set.
    /// By subclassing, we can can programmatically generate any text desired.
    /// </summary>
    public class CustomPatternLayout : PatternLayout
    {
        //Datetime.Now.ToString("s"): 
        /*The "s" standard format specifier represents a custom date and time format string that 
        is defined by the DateTimeFormatInfo.SortableDateTimePattern property. The pattern reflects 
        a defined standard (ISO 8601), and the property is read-only. Therefore, it is always the same, 
        regardless of the culture used or the format provider supplied. 
        The custom format string is "yyyy'-'MM'-'dd'T'HH':'mm':'ss".
        [...]
        When this standard format specifier is used, the formatting or parsing operation always uses the invariant culture.
        -- https://msdn.microsoft.com/en-us/library/az4se3k1.aspx#Sortable
        */

        public override string Header
        {
            get
            {
                var assembly = Assembly.GetEntryAssembly().GetName();
                return $"\nStarting up {assembly.Name} Version {assembly.Version} on {DateTime.Now.ToString("s")}\n";
            }
        }

        public override string Footer
        {
            get
            {
                return $"Stopping on {DateTime.Now.ToString("s")}\n";
            }
        }
    }
}