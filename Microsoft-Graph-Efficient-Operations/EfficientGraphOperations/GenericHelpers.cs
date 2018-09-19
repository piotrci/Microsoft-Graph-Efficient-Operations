using Microsoft.Graph;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EfficientRequestHandling
{
    public static class GenericHelpers
    {
        public static T ReturnNoContent<T>(this T request) where T : IBaseRequest
        {
            request.Header("prefer", "return-no-content");
            return request;
        }
        public static void SplitIntoCollections<T>(this IEnumerable<T> input, Func<T, bool> splitPredicate, out ICollection<T> trueCollection, out ICollection<T> falseCollection)
        {
            trueCollection = new List<T>();
            falseCollection = new List<T>();
            foreach (var item in input)
            {
                if (splitPredicate(item))
                {
                    trueCollection.Add(item);
                }
                else
                {
                    falseCollection.Add(item);
                }
            }
        }
        /// <summary>
        /// Generates "partitions" for properties that are alphanumeric strings longer than 1 character
        /// </summary>
        /// <remarks>This approach works when we can assume that the property is longer than 1 character, so we can safely use the 'le' and 'ge' operators and know we catch everything</and></remarks>
        /// <returns></returns>
        public static IEnumerable<string> GenerateFilterRangesForAlphaNumProperties(string propertyName)
        {
            char rangeStart = 'a';
            char rangeEnd = 'z';
            var rangeString = "{0} ge '{1}' and {0} le '{2}'";
            var ranges = new List<string>
            {
                // this assumes that REST api is case insensitive
                // first range is everything under a
                $"{propertyName} le '{rangeStart}'"
            };

            // now make a range for every lower case letter in the alphabet, (a,b), (b, c), (c, d)
            for (char charValue = rangeStart; charValue < rangeEnd; charValue++)
            {
                ranges.Add(String.Format(rangeString, propertyName, charValue, (char)(charValue + 1)));
            }
            // last range is everything above z
            ranges.Add($"{propertyName} ge '{rangeEnd}'");

            return ranges;
        }
        /// <summary>
        /// Generates partitions for time ranges, like when getting email messages for a time range.
        /// </summary>
        /// <param name="propertyName"></param>
        /// <remarks>Granularity of the filter expression is 1 day. So we take the time range and devide it into round day ranges, with a max number of ranges.</remarks>
        /// <returns></returns>
        public static IEnumerable<string> GenerateFilterRangesForDateRange(string propertyName, DateTime start, DateTime end, int maxRanges)
        {
            double noOfDays = Math.Ceiling((end - start).TotalDays);
            int daysInRange = (int)Math.Ceiling(noOfDays / maxRanges);

            var rangeString = "{0} ge {1} and {0} le {2}";
            var ranges = new List<string>();

            DateTime startRange = start;
            while (startRange < end)
            {
                DateTime endRange = startRange.AddDays(daysInRange);
                endRange = endRange < end ? endRange : end;
                ranges.Add(String.Format(rangeString, propertyName, startRange.ToString("yyyy-MM-dd"), endRange.ToString("yyyy-MM-dd")));
                startRange = endRange;
            }
            return ranges;
        }
        public static IEnumerable<T[]> SplitIntoBatches<T>(this IEnumerable<T> items, int batchSize)
        {
            using (var enumerator = items.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    yield return MakeBatch(enumerator, batchSize).ToArray();
                }
            }
        }
        private static IEnumerable<T> MakeBatch<T>(IEnumerator<T> enumerator, int batchSize)
        {
            // return first item because caller already moved one
            yield return enumerator.Current;
            int itemsReturned = 1;
            while (itemsReturned < batchSize && enumerator.MoveNext())
            {
                itemsReturned++;
                yield return enumerator.Current;
            }
        }

        public static bool TryDequeue<T>(this Queue<T> queue, out T item)
        {
            try
            {
                item = queue.Dequeue();
                return true;
            }
            catch (InvalidOperationException)
            {
                item = default(T);
                return false; ;
            }
        }
        public static bool Contains(this string s, string x, StringComparison comparison)
        {
            if (s == null)
            {
                return false;
            }
            return s.IndexOf(x, comparison) > -1;
        }

        public class EntityComparer : IEqualityComparer<Entity>
        {
            public bool Equals(Entity x, Entity y)
            {
                return String.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase);
            }

            public int GetHashCode(Entity obj)
            {
                return obj.Id.GetHashCode();
            }
        }

        private static readonly Random randomGenerator = new Random();
        public static string GenerateRandomEntityName()
        {
            int num = randomGenerator.Next(0, 26); // Zero to 25
            char letter = (char)('a' + num);
            return letter + System.IO.Path.GetRandomFileName().Replace(".", String.Empty);
        }
    }
}
