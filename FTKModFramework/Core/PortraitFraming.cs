using System;

namespace FTKModFramework.Core
{
    // Camera-axis extents of the posed mesh. Pure arithmetic is independently testable.
    internal static class PortraitFraming
    {
        internal static bool Fit(double x,double y,double z,double fov,int width,int height,double near,double far,out double distance,out double scale)
        {
            distance=0;scale=1;
            if(width<=0||height<=0)return false;
            double aspect=(double)width/height;
            if(Distance(x,y,z,fov,aspect,near,far,out distance))return true;
            // Reduce only the disposable portrait presentation, never enlarge or shrink below 10%.
            if(!Distance(x,y,z,fov,aspect,near,1e12,out distance))return false;
            scale=(far*.95)/(distance+z);
            if(scale<.1||scale>=1||double.IsNaN(scale)||double.IsInfinity(scale))return false;
            return Distance(x*scale,y*scale,z*scale,fov,aspect,near,far,out distance);
        }
        internal static bool Distance(double x,double y,double z,double fov,double aspect,double near,double far,out double distance)
        {
            distance=0;
            foreach(double value in new[]{x,y,z,fov,aspect,near,far})
                if(double.IsNaN(value)||double.IsInfinity(value))return false;
            if(x<=0||y<=0||z<0||fov<=0||fov>=170||aspect<=0||near<=0||far<=near)return false;
            double tangent=Math.Tan(fov*Math.PI/360);
            distance=z+Math.Max(Math.Max(x/(tangent*aspect),y/tangent)*1.05,near*1.05);
            return distance+z<far*.99;
        }
    }
}
