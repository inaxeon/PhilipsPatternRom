using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhilipsPatternRom.Converter.Extensions
{
    public static class Extension
    {
        public static bool HasDuplicate<T>(
            this IEnumerable<T> source,
            out T firstDuplicate)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            var checkBuffer = new HashSet<T>();
            foreach (var t in source)
            {
                if (checkBuffer.Add(t))
                {
                    continue;
                }

                firstDuplicate = t;
                return true;
            }

            firstDuplicate = default(T);
            return false;
        }
    }
}
