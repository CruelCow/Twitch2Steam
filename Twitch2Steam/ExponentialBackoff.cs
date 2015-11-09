using System;
using static System.Math;

namespace Twitch2Steam
{
    //TODO: validate Timespans. (Overflow, negative values)
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
            this.InitialDelay = InitialDelay;
            this.Factor = Factor;
            this.MaximumDelay = MaximumDelay;
            this.MaximumJitter = MaximumJitter;
            Reset();

            rng = new Random();
        }

        public TimeSpan Reset()
        {
            nextDelay = InitialDelay.TotalMilliseconds;
            return InitialDelay;
        }

        public TimeSpan NextDelay
        {
            get
            {
                //grow exponentially
                //overflows if next Delay is ~universe age
                nextDelay = Min(nextDelay * Factor, MaximumDelay.TotalMilliseconds);

                //add jitter
                //overflows if MaximumJitter is ~25 Days or more
                nextDelay += rng.Next((int)MaximumJitter.TotalMilliseconds);
                
                return TimeSpan.FromMilliseconds(nextDelay);
            }
        }
    }
}
