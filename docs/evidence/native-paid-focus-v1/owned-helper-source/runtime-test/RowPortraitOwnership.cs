using System;
using System.Collections.Generic;

// Pure positive ownership gate. A shared pre-call native texture can never enter cleanup's owned slot.
internal static class RowPortraitOwnership
{
    internal static int NativeDimension(float rectSize,int aa)
    {
        if(float.IsNaN(rectSize)||float.IsInfinity(rectSize)||rectSize<1||rectSize>1024||aa<1||aa>8)throw new ArgumentException("Native rectangle/AA outside bounded fixture contract.");
        int size=checked((int)rectSize*aa);
        if(size<1||size>1024)throw new ArgumentException("Native texture dimension outside1..1024.");
        return size;
    }
    internal static bool Fresh(int instanceId,int width,int height,HashSet<int> preexisting,int expectedWidth=204,int expectedHeight=172)
    {
        return preexisting!=null && preexisting.Count>0 && instanceId!=0 && expectedWidth>0 && expectedWidth<=1024 && expectedHeight>0 && expectedHeight<=1024 && width==expectedWidth && height==expectedHeight && !preexisting.Contains(instanceId);
    }
}
