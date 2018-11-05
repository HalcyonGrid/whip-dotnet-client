using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace whipstress
{
    public class TestUtil
    {
        public class Test
        {
            public static bool test(byte[] A, byte[] B)
            {
                if (A.Length != B.Length)
                    return false;
                for (int i = 0; i < A.Length; i++)
                {
                    if (A[i] != B[i])
                        return false;
                }
                return true;
            }
        }

        public static byte[] RandomBytes()
        {
            Random random = new Random();
            System.Collections.Generic.List<byte> randomAsset = new System.Collections.Generic.List<byte>();
            int numBytes = random.Next(2000, 2000000);

            for (int i = 0; i < numBytes; i++)
            {
                randomAsset.Add((byte)Math.Floor(26 * random.NextDouble() + 65));
            }

            return randomAsset.ToArray();
        }

        public static byte[] RandomBytes(int min, int max)
        {
            Random random = new Random();
            System.Collections.Generic.List<byte> randomAsset = new System.Collections.Generic.List<byte>();
            int numBytes = random.Next(min, max);

            for (int i = 0; i < numBytes; i++)
            {
                randomAsset.Add((byte)Math.Floor(26 * random.NextDouble() + 65));
            }

            return randomAsset.ToArray();
        }
    }
}
