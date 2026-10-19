using System.Text;

namespace YamahaAvrNet.TesterGui
{
    internal static class NetFrameworkCompat
    {
        public static string Replace(string value, string oldValue, string newValue, StringComparison comparisonType)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
                // <-----------
            }

            if (oldValue is null)
            {
                throw new ArgumentNullException(nameof(oldValue));
                // <-----------
            }

            if (oldValue.Length == 0)
            {
                throw new ArgumentException("String cannot be of zero length.", nameof(oldValue));
                // <-----------
            }

            int matchIndex = value.IndexOf(oldValue, comparisonType);
            if (matchIndex < 0)
            {
                return value;
                // <-----------
            }

            StringBuilder builder = new StringBuilder();
            int copyStart = 0;
            while (matchIndex >= 0)
            {
                builder.Append(value, copyStart, matchIndex - copyStart);
                builder.Append(newValue);
                copyStart = matchIndex + oldValue.Length;
                matchIndex = value.IndexOf(oldValue, copyStart, comparisonType);
            }

            builder.Append(value, copyStart, value.Length - copyStart);
            return builder.ToString();
            // <-----------
        }
    }
}
