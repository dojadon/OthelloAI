using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace OthelloAI
{
    public class Regression
    {
        public static (float a, float b) Exponential(IEnumerable<float> x, IEnumerable<float> y)
        {
            (float a, float b) = Linear(x, y.Select(yi => (float) Math.Log(yi + 0.001F)));
            return (a, (float)Math.Exp(b));
        }

        public static (float a, float b) Linear(IEnumerable<float> x, IEnumerable<float> y)
        {
            float ax = x.Average();
            float ay = y.Average();

            float axy = x.Zip(y, (xi, yi) => xi * yi).Average();
            float axx = x.Select(x => x * x).Average();

            float a = (axy - ax * ay) / (axx - ax * ax);
            float b = -a * ax + ay;

            return (a, b);
        }
    }
}
