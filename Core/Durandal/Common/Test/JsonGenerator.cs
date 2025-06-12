using Durandal.Common.MathExt;
using Durandal.Common.Utils;
using System;
using System.Collections.Generic;
using System.Text;

namespace Durandal.Common.Test
{
    public static class JsonGenerator
    {
        public static string CreateJsonHedgemaze(IRandom rand, int tokenLimit, int depthLimit = 6)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                CreateJsonHedgemaze(pooledSb.Builder, rand, tokenLimit, depthLimit);
                return pooledSb.Builder.ToString();
            }
        }

        public static void CreateJsonHedgemaze(StringBuilder builder, IRandom rand, int tokenLimit, int depthLimit = 6)
        {
            int localLimit = tokenLimit;
            CreateJsonHedgemaze(builder, rand, ref localLimit, 0, depthLimit);
        }

        private static string GenerateRandomStringToken(IRandom rand)
        {
            using (PooledStringBuilder pooledSb = StringBuilderPool.Rent())
            {
                GenerateRandomStringToken(rand, pooledSb.Builder);
                return pooledSb.Builder.ToString();
            }
        }

        private static void GenerateRandomStringToken(IRandom rand, StringBuilder builder)
        {
            int len = rand.NextInt(1, 20);
            for (int c = 0; c < len; c++)
            {
                if (rand.NextFloat() < 0.2)
                {
                    builder.Append((char)('0' + rand.NextInt(0, 10)));
                }
                else
                {
                    builder.Append((char)('a' + rand.NextInt(0, 26)));
                }
            }
        }

        private static void CreateJsonHedgemaze(StringBuilder builder, IRandom rand, ref int tokenLimit, int depth, int depthLimit)
        {
            HashSet<string> keysUsedAtThisLevel = new HashSet<string>();
            builder.Append(' ', depth);
            builder.Append("{\r\n");
            int numKeysToAttempt = rand.NextInt(3, 20);
            if (depth == 0)
            {
                numKeysToAttempt = int.MaxValue;
            }

            depth += 1;
            for (int keyIdx = 0; keyIdx < numKeysToAttempt && tokenLimit >= 0; keyIdx++)
            {
                string key = GenerateRandomStringToken(rand);
                while (keysUsedAtThisLevel.Contains(key))
                {
                    key = GenerateRandomStringToken(rand);
                }

                keysUsedAtThisLevel.Add(key);

                // Write the property name
                builder.Append(' ', depth);
                builder.Append("\"");
                builder.Append(key);
                builder.Append("\":");
                tokenLimit--;

                int typeOfValue = rand.NextInt(0, 11); // upper limit is arbitrary, just depends on how many string values we want to skew towards
                if (depth >= depthLimit)
                {
                    typeOfValue = -1; // force string value if we're at depth limit
                }

                // Now the value
                switch (typeOfValue)
                {
                    case 0:
                    case 1:
                    case 2:
                        // Array of something
                        int numValuesToAttempt = rand.NextInt(0, 10);
                        builder.Append("\r\n");
                        builder.Append(' ', depth);
                        builder.Append("[\r\n");
                        depth += 1;
                        for (int arrayIdx = 0; arrayIdx < numValuesToAttempt && tokenLimit >= 0; arrayIdx++)
                        {
                            if (typeOfValue == 0)
                            {
                                // Array of integers
                                builder.Append(' ', depth);
                                builder.Append(rand.NextInt(0, 1000));
                                tokenLimit--;
                            }
                            else if (typeOfValue == 1)
                            {
                                // Array of strings
                                builder.Append(' ', depth);
                                builder.Append("\"");
                                GenerateRandomStringToken(rand, builder);
                                builder.Append("\"");
                                tokenLimit--;
                            }
                            else
                            {
                                // Array of objects
                                CreateJsonHedgemaze(builder, rand, ref tokenLimit, depth, depthLimit);
                            }

                            if (arrayIdx >= numValuesToAttempt || tokenLimit < 0)
                            {
                                builder.Append("\r\n");
                            }
                            else
                            {
                                builder.Append(",\r\n");
                            }
                        }
                        depth -= 1;
                        builder.Append(' ', depth);
                        builder.Append("]");
                        break;
                    case 3:
                        // Null value
                        builder.Append(" null");
                        tokenLimit--;
                        break;
                    case 4:
                        // Int value
                        builder.Append(" ");
                        builder.Append(rand.NextInt(1, 1000));
                        tokenLimit--;
                        break;
                    case 5:
                    case 6:
                    case 7:
                        // Nested single object
                        builder.Append("\r\n");
                        CreateJsonHedgemaze(builder, rand, ref tokenLimit, depth, depthLimit);
                        break;
                    default:
                        // string
                        builder.Append(" \"");
                        GenerateRandomStringToken(rand, builder);
                        builder.Append("\"");
                        tokenLimit--;
                        break;
                }

                // Omit the comma if we won't loop again...
                if (keyIdx >= numKeysToAttempt || tokenLimit < 0)
                    builder.Append("\r\n");
                else
                    builder.Append(",\r\n");
            }

            depth -= 1;
            builder.Append(' ', depth);
            builder.Append("}");
        }
    }
}
