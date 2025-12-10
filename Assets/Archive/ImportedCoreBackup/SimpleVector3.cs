namespace MyGame_1.Core
{
    public struct SimpleVector3
    {
        public float x;
        public float y;
        public float z;

        public SimpleVector3(float x, float y, float z)
        {
            this.x = x;
            this.y = y;
            this.z = z;
        }

        public static SimpleVector3 Zero => new SimpleVector3(0f, 0f, 0f);
    }
}
