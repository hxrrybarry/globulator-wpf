namespace globulator;

// nicked this from https://www.codeproject.com/articles/328740/binary-operations-on-byte-arrays-with-parallelism

public static class BinOps
{
    private const int BITS_IN_BYTE = 8;
    private static readonly int parallelDegree;
    private static readonly int uintSize;
    private static readonly int bits_in_uint;
    static BinOps()
    {
        parallelDegree = Environment.ProcessorCount;
        uintSize = sizeof(uint) / sizeof(byte); // really paranoid, uh ? // lmao not sure what he meant by this - Harry
        bits_in_uint = uintSize * BITS_IN_BYTE;
    }

    public static byte[] Bin_Xor(this byte[] ba, byte[] bt)
    {
        int lenBig = Math.Max(ba.Length, bt.Length);
        int lenSmall = Math.Min(ba.Length, bt.Length);
        byte[] result = new byte[lenBig];
        int ipar = 0;
        object o = new();
        System.Action paction = delegate ()
        {
            int actidx;
            lock (o)
            {
                actidx = ipar++;
            }
            unsafe
            {
                fixed (byte* ptres = result, ptba = ba, ptbt = bt)
                {
                    uint* pr = ((uint*)ptres) + actidx;
                    uint* pa = ((uint*)ptba) + actidx;
                    uint* pt = ((uint*)ptbt) + actidx;
                    while (pr < ptres + lenSmall)
                    {
                        *pr = (*pt ^ *pa);
                        pr += parallelDegree; pa += parallelDegree; pt += parallelDegree;
                    }
                    uint* pl = ba.Length > bt.Length ? pa : pt;
                    while (pr < ptres + lenBig)
                    {
                        *pr = *pl;
                        pr += parallelDegree; pl += parallelDegree;
                    }
                }
            }
        };
        System.Action[] actions = new Action[parallelDegree];
        for (int i = 0; i < parallelDegree; i++)
            actions[i] = paction;
        Parallel.Invoke(actions);

        return result;
    }
}