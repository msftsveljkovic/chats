namespace API
{
    class Elem
    {
        public long ts { get ; set ; }
        public int load { get; set ; }
    }

    public class Meter
    {
        private Queue<Elem> elems = new Queue<Elem>();
        private readonly TimeSpan period = TimeSpan.FromSeconds(30);
        private int load;

        public int Refresh()
        {
            var now = DateTime.Now;
            while (elems.Count > 0)
            {
                // Not sure why, but `elems.Peek()` is sometimes `null`
                var el = elems.Peek();
                if (el == null)
                {
                    elems.Dequeue();
                    continue;
                }
                var elapsed = TimeSpan.FromTicks(now.Ticks - el.ts);
                if (elapsed > period)
                {
                    var sample = elems.Dequeue();
                    // this is _really_ weird that this can be null also!
                    if (sample != null)
                        load -= sample.load;
                }
                else break;
            }
            return load;
        }

        public int Sample(int value)
        {
            Refresh();
            load += value;
            elems.Enqueue(new Elem { ts = DateTime.Now.Ticks, load = value });
            return load;
        }
    }
}
