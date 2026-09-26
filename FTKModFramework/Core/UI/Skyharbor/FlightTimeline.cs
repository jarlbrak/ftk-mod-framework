using System;

namespace FTKModFramework.Core.UI.Skyharbor
{
    // Pure presentation choreography; buoyant lift clears the quay before powered turns.
    internal sealed class FlightPose
    {
        public string phase;
        public double time, x, y, z, yaw, bank, gangwayAngle, mooringScale, propellerSpeed, propellerAngle;
        public bool attached;
    }

    internal static class FlightTimeline
    {
        public const double Duration = 120;
        public const double LiftHeight = 8;
        static double Ease(double t) { t = Math.Max(0, Math.Min(1, t)); return t*t*t*(10+t*(-15+6*t)); }
        static double EaseIntegral(double t) { return 2.5*t*t*t*t-3*t*t*t*t*t+t*t*t*t*t*t; }
        static double Clamp01(double t) { return Math.Max(0,Math.Min(1,t)); }

        static void Airborne(FlightPose p,double t)
        {
            // Parameterizing by heading avoids the angular spikes of a sheared ellipse's
            // ordinary parameter. One envelope controls the whole loop, never each leg.
            double u=Clamp01((t-38)/58);
            double heading=2*Math.PI*Ease(u);
            const double radius=24, depth=26, forwardOffset=12;
            double sine=Math.Sin(heading),cosine=Math.Cos(heading);
            double a=depth*sine,b=radius*cosine+forwardOffset*sine;
            double denominator=Math.Sqrt(a*a+b*b);
            double sinTheta=a/denominator,cosTheta=b/denominator;
            p.x=-radius*(1-cosTheta);
            p.z=forwardOffset*(1-cosTheta)-depth*sinTheta;
            double lift=12*Ease((t-28)/14)*Ease((106-t)/14);
            double airborne=Clamp01((t-28)/78);
            double heaveEnvelope=Math.Pow(Math.Sin(Math.PI*airborne),4);
            p.y=lift+9*(1-cosTheta)+.12*heaveEnvelope*Math.Sin(2*Math.PI*(t-28)/9);
            p.yaw=heading*180/Math.PI;
            p.bank=1.6*Math.Pow(Math.Sin(Math.PI*u),4);
            p.phase=t<38?"buoyant-lift":t<50?"forward-turn":t<66?"depart"
                :t<78?"wide-turn":t<96?"return":"buoyant-descent";
        }

        static void Propellers(FlightPose p,double t)
        {
            // Integer rotations per cycle preserve rotor orientation at the visible docked wrap.
            const double idle=30, cruise=700, delta=cruise-idle;
            const double rawCycle=55190;
            const double scale=55080/rawCycle;
            double angle,speed;
            if(t<20) { angle=idle*t;speed=idle; }
            else if(t<36)
            {
                double u=(t-20)/16;angle=600+idle*(t-20)+delta*16*EaseIntegral(u);speed=idle+delta*Ease(u);
            }
            else if(t<98) { angle=6440+cruise*(t-36);speed=cruise; }
            else if(t<112)
            {
                double u=(t-98)/14;angle=49840+cruise*(t-98)-delta*14*EaseIntegral(u);speed=cruise-delta*Ease(u);
            }
            else { angle=54950+idle*(t-112);speed=idle; }
            p.propellerAngle=(angle*scale)%360;p.propellerSpeed=speed*scale;
        }

        public static FlightPose Evaluate(double seconds)
        {
            if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0) throw new ArgumentOutOfRangeException("seconds");
            double t=seconds%Duration;
            var p=new FlightPose { time=t,phase="docked",mooringScale=1,attached=true };
            Propellers(p,t);
            if(t<20||t>=116) return p;
            p.attached=false;
            if(t<24) { p.phase="reel-lines";p.mooringScale=1-Ease((t-20)/4);return p; }
            p.mooringScale=0;
            if(t<28) { p.phase="raise-gangway";p.gangwayAngle=-85*Ease((t-24)/4);return p; }
            p.gangwayAngle=-85;
            if(t<106) { Airborne(p,t);return p; }
            if(t<112) { p.phase="lower-gangway";p.gangwayAngle=-85*(1-Ease((t-106)/6));return p; }
            p.gangwayAngle=0;p.phase="restore-lines";p.mooringScale=Ease((t-112)/4);return p;
        }
    }
}
