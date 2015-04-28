using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TcxSplitCalculator
{
    public static class CalculationHelpers
    {
        public static double MetersToMiles(double meters)
        {
            return meters * 0.00062137;
        }

        public static double MetersToKilometers(double meters)
        {
            return meters / 1000;
        }
    }
}
