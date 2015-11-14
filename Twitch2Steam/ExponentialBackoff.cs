using System;
using static System.Math;

namespace Twitch2Steam
{
    public class ExponentialBackoff
    {
        public TimeSpan InitialDelay { get; private set; }
        public double Factor { get; private set; }
        public TimeSpan MaximumDelay { get; private set; }
        public TimeSpan MaximumJitter { get; private set; }
        private double nextDelay;

        private readonly Random rng;

        public ExponentialBackoff() : this(TimeSpan.FromSeconds(1), 2, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(1000))
        { }

        public ExponentialBackoff( TimeSpan InitialDelay, double Factor, TimeSpan MaximumDelay, TimeSpan MaximumJitter )
        {
            if (InitialDelay <= TimeSpan.Zero)
                throw new ArgumentException("Illegal TimeSpan", nameof(InitialDelay));

            if (MaximumDelay <= TimeSpan.Zero)
                throw new ArgumentException("Illegal TimeSpan", nameof(MaximumDelay));

            if (MaximumJitter < TimeSpan.Zero)
                throw new ArgumentException("Illegal TimeSpan", nameof(MaximumJitter));

            if (Factor <= 1)
                throw new ArgumentException($"Illegal {nameof(Factor)}");

            if (MaximumDelay < InitialDelay)
                throw new ArgumentException($"{nameof(MaximumDelay)} must be bigger than {nameof(InitialDelay)}");

            this.InitialDelay = InitialDelay;
            this.Factor = Factor;
            this.MaximumDelay = MaximumDelay;
            this.MaximumJitter = MaximumJitter;
            Reset();

            rng = new Random();
        }

        public TimeSpan Reset()
        {
            nextDelay = 0;
            return TimeSpan.Zero;
        }

        public TimeSpan NextDelay
        {
            get
            {
                checked
                {
                    if (nextDelay == 0)
                    {
                        nextDelay = InitialDelay.TotalMilliseconds;
                        return TimeSpan.Zero;
                    }
                    else
                    {
                        nextDelay = Min(nextDelay * Factor, MaximumDelay.TotalMilliseconds);

                        nextDelay += rng.Next(( int )MaximumJitter.TotalMilliseconds);

                        return TimeSpan.FromMilliseconds(nextDelay);
                    }
                }
            }
        }
    }
}
