using System;
using FTKModFramework.Core.UI.Skyharbor;
class Program
{
    static void Assert(bool value,string message) { if(!value) throw new Exception(message); }
    static double AngleDelta(double a,double b) { double d=(b-a)%360;if(d>180)d-=360;if(d< -180)d+=360;return d; }
    static FlightPose At(double t) { return FlightTimeline.Evaluate((t+FlightTimeline.Duration)%FlightTimeline.Duration); }
    static double[] Position(double t) { var p=At(t);return new[]{p.x,p.y,p.z}; }
    static double[] Velocity(double t) { const double h=.001;var a=Position(t-h);var b=Position(t+h);return new[]{(b[0]-a[0])/(2*h),(b[1]-a[1])/(2*h),(b[2]-a[2])/(2*h)}; }
    static double[] Acceleration(double t) { const double h=.001;var a=Position(t-h);var b=Position(t);var c=Position(t+h);return new[]{(c[0]-2*b[0]+a[0])/(h*h),(c[1]-2*b[1]+a[1])/(h*h),(c[2]-2*b[2]+a[2])/(h*h)}; }
    static double Norm(double[] a) { return Math.Sqrt(a[0]*a[0]+a[1]*a[1]+a[2]*a[2]); }
    static double Difference(double[] a,double[] b) { return Norm(new[]{a[0]-b[0],a[1]-b[1],a[2]-b[2]}); }
    static double YawRate(double t) { return AngleDelta(At(t-.001).yaw,At(t+.001).yaw)/.002; }
    static void Main(string[] args)
    {
        if(args.Length==2) AssetPackage.Verify(args[0],args[1]);
        double maxSpeed=0,maxAcceleration=0,maxYawRate=0,minCruiseSpeed=double.MaxValue;
        for(double t=0;t<FlightTimeline.Duration;t+=.01)
        {
            var p=At(t);var velocity=Velocity(t);
            Assert(double.IsFinite(p.x+p.y+p.z+p.yaw+p.bank+p.propellerAngle+p.propellerSpeed),"Nonfinite pose");
            Assert(p.x<=.00001,"Path enters dock side");
            Assert(p.mooringScale>=0&&p.mooringScale<=1,"Invalid rope scale");
            Assert(p.gangwayAngle>=-85&&p.gangwayAngle<=0,"Invalid bridge angle");
            Assert(p.propellerSpeed>0&&p.propellerSpeed<=700,"Invalid rotor speed");
            if(Math.Abs(p.x)+Math.Abs(p.y)+Math.Abs(p.z)>.000001)
                Assert(p.mooringScale==0&&p.gangwayAngle==-85,"Ship moves with attachment deployed");
            if(p.y<FlightTimeline.LiftHeight-.000001)
                Assert(Math.Abs(p.x)+Math.Abs(p.z)<.000001&&Math.Abs(AngleDelta(p.yaw,0))<.00001,"Horizontal motion or turn below quay clearance");
            if(t>38.01&&t<95.99)
            {
                double speed=Math.Sqrt(velocity[0]*velocity[0]+velocity[2]*velocity[2]);
                if(speed>.000001)
                {
                    double yaw=p.yaw*Math.PI/180;
                    Assert((-Math.Sin(yaw)*velocity[0]-Math.Cos(yaw)*velocity[2])/speed>.9999,"Heading differs from forward tangent at "+t);
                }
            }
            maxSpeed=Math.Max(maxSpeed,Norm(velocity));maxAcceleration=Math.Max(maxAcceleration,Norm(Acceleration(t)));
            maxYawRate=Math.Max(maxYawRate,Math.Abs(YawRate(t)));
            if(t>38&&t<96) minCruiseSpeed=Math.Min(minCruiseSpeed,Norm(velocity));
        }
        Assert(maxSpeed<12&&maxAcceleration<4&&maxYawRate<12,"Cruise motion exceeds calm presentation bounds");
        Assert(minCruiseSpeed>.1,"Unintended stop between lift and descent");
        foreach(double boundary in new double[]{20,24,28,36,38,42,50,66,78,92,96,98,106,112,116,120})
        {
            double left=boundary-.0001,right=boundary+.0001;
            var a=At(left);var b=At(right);
            Assert(Difference(Position(left),Position(right))<.003,"Position discontinuity "+boundary);
            Assert(Difference(Velocity(left),Velocity(right))<.005,"Velocity discontinuity "+boundary);
            Assert(Difference(Acceleration(left),Acceleration(right))<.01,"Acceleration discontinuity "+boundary);
            Assert(Math.Abs(YawRate(left)-YawRate(right))<.01,"Angular velocity discontinuity "+boundary);
            Assert(Math.Abs(AngleDelta(a.yaw,b.yaw))<.01&&Math.Abs(a.bank-b.bank)<.01,"Orientation discontinuity "+boundary);
            Assert(Math.Abs(AngleDelta(a.propellerAngle,b.propellerAngle))<.2,"Rotor discontinuity "+boundary);
            Assert(Math.Abs(a.propellerSpeed-b.propellerSpeed)<.02,"Throttle discontinuity "+boundary);
            Assert(Math.Abs(a.gangwayAngle-b.gangwayAngle)<.01&&Math.Abs(a.mooringScale-b.mooringScale)<.001,"Attachment discontinuity "+boundary);
        }
        var start=FlightTimeline.Evaluate(0);var wrap=FlightTimeline.Evaluate(FlightTimeline.Duration);
        Assert(start.x==wrap.x&&start.y==wrap.y&&start.z==wrap.z&&start.yaw==wrap.yaw&&start.bank==wrap.bank&&start.gangwayAngle==wrap.gangwayAngle&&start.mooringScale==wrap.mooringScale&&start.propellerAngle==wrap.propellerAngle&&wrap.attached,"Cycle wrap differs");
        Console.WriteLine("Smooth flight checks passed. Max speed {0:F3}, acceleration {1:F3}, yaw rate {2:F3}; minimum cruise speed {3:F3}.",maxSpeed,maxAcceleration,maxYawRate,minCruiseSpeed);
    }
}
