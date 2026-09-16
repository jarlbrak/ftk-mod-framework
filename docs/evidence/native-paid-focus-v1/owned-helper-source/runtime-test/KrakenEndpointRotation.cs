using System;

// System-only so the actual scalar interpolation can be tested without a Unity runtime.
internal static class KrakenEndpointRotation
{
    static double[] Unit(double[] value)
    {
        if(value==null || value.Length!=4)throw new ArgumentException("Quaternion requires four components.");
        double norm=0;
        for(int i=0;i<4;i++)
        {
            if(double.IsNaN(value[i]) || double.IsInfinity(value[i]))throw new ArgumentException("Nonfinite quaternion.");
            norm+=value[i]*value[i];
        }
        if(double.IsInfinity(norm) || norm<1e-24)throw new ArgumentException("Invalid quaternion norm.");
        norm=Math.Sqrt(norm);double[] result=new double[4];
        for(int i=0;i<4;i++)result[i]=value[i]/norm;
        return result;
    }
    public static double[] Interpolate(double[] from,double[] to,double weight)
    {
        if(double.IsNaN(weight) || double.IsInfinity(weight) || weight<0 || weight>1)
            throw new ArgumentException("SLERP weight must be finite and in [0,1].");
        double[] a=Unit(from),b=Unit(to);double dot=0;
        for(int i=0;i<4;i++)dot+=a[i]*b[i];
        if(dot<0){dot=-dot;for(int i=0;i<4;i++)b[i]=-b[i];}
        dot=Math.Min(1,Math.Max(0,dot));
        double left,right;
        // Only the numerically coincident limit uses linear coefficients. No close-angle cutoff.
        if(dot==1){left=1-weight;right=weight;}
        else
        {
            double angle=Math.Acos(dot),denominator=Math.Sin(angle);
            left=Math.Sin((1-weight)*angle)/denominator;
            right=Math.Sin(weight*angle)/denominator;
        }
        double[] result=new double[4];for(int i=0;i<4;i++)result[i]=left*a[i]+right*b[i];
        return Unit(result);
    }
}
