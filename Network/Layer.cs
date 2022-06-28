namespace OthelloAI.Network
{
    public struct SparseVectorElement
    {
        public int index;
        public float value;

        public SparseVectorElement(int index, float value)
        {
            this.index = index;
            this.value = value;
        }
    }

    public class LayerRegionSelection
    {
        public float[] weights;

        public LayerRegion LayerRegion { get; }

        public LayerRegionSelection(LayerRegion layer)
        {
            weights = new float[layer.Size];
        }

        public SparseVectorElement[] Eval(int[] b)
        {
            int Hash(int[] a)
            {
                return 0;
            }

            int hash = Hash(b);

            var result = new SparseVectorElement[1 + weights.Length * 2];
            result[0] = new SparseVectorElement(hash, 0);

            for (int i = 0; i < weights.Length; i++)
            {
                float w = weights[i];
                float w1 = (1 + 2 * w) / 3F / weights.Length;
                float w2 = (1 - w) / 3F / weights.Length;

                result[0].value += w1;

                int stone = b[i];

                b[i] = (stone + 2) % 3 - 1;
                result[i * 2 + 1] = new SparseVectorElement(Hash(b), w2);

                b[i] = (stone + 3) % 3 - 1;
                result[i * 2 + 2] = new SparseVectorElement(Hash(b), w2);

                b[i] = stone;
            }

            return null;
        }
    }

    public class LayerRegion
    {
        public int Size { get; }
    }
}
