//**********************************************
// Project: Flood Fill Algorithms in C# & GDI+
// File Description: Flood Fill Class
//
// Copyright: Copyright 2003 by Justin Dunlap.
//    Any code herein can be used freely in your own 
//    applications, provided that:
//     * You agree that I am NOT to be held liable for
//       any damages caused by this code or its use.
//     * You give proper credit to me if you re-publish
//       this code.
//**********************************************

using System.Drawing;
using System.Drawing.Imaging;

namespace OnTopReplica
{
	public class FloodFiller
    {
        public class FloodFillRectangle
        {
            public int MinX { get; set; }

            public int MaxX { get; set; }

            public int MinY { get; set; }

            public int MaxY { get; set; }

            public FloodFillRectangle()
            {
                MinX = int.MaxValue;
                MaxX = int.MinValue;
                MinY = int.MaxValue;
                MaxY = int.MinValue;
            }
        }


        ///<summary>initializes the FloodFill operation</summary>
        public static FloodFillRectangle FloodFill(Bitmap bmp, Point pt, Color color)
	    {
            var c = ToBgra(color);
            var rect = new FloodFillRectangle();

            //get the bits
            var bmpData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
	        unsafe
	        {
	            //resolve pointer
	            var scan0 = (byte*) (void*)bmpData.Scan0;
	            //get the starting color
	            //[loc += Y offset + X offset]
	            var loc = CoordsToIndex(pt.X, pt.Y, bmpData.Stride); //((bmpData.Stride*(pt.Y-1))+(pt.X*4));
	            var startColor = *((int*) (scan0 + loc));

	            //create the array of bools that indicates whether each pixel
	            //has been checked.  (Should be bitfield, but C# doesn't support bitfields.)
	            var pixelsChecked = new bool[bmpData.Width + 1, bmpData.Height + 1];
	            LinearFloodFill4(scan0, pt.X, pt.Y, new Size(bmpData.Width, bmpData.Height), bmpData.Stride, (byte*) &startColor, c, pixelsChecked, ref rect);
	        }
	        bmp.UnlockBits(bmpData);
            return rect;
	    }

	    //***********
		//LINEAR ALGORITHM
		//***********
		
		private static unsafe void LinearFloodFill4( byte* scan0, int x, int y,Size bmpsize, int stride, byte* startcolor, int color, bool[,] pixelsChecked, ref FloodFillRectangle rect)
		{
			
			//offset the pointer to the point passed in
			var p=(int*) (scan0+(CoordsToIndex(x,y, stride)));
			
			
			//FIND LEFT EDGE OF COLOR AREA
			var lFillLoc=x; //the location to check/fill on the left
			var ptr=p; //the pointer to the current location
			while(true)
			{
				ptr[0]=color; 	 //fill with the color
				pixelsChecked[lFillLoc,y]=true;
				lFillLoc--; 		 	 //de-increment counter
				ptr-=1;				 	 //de-increment pointer
				if(lFillLoc<=0 || !CheckPixel((byte*)ptr,startcolor) ||  (pixelsChecked[lFillLoc,y]))
					break;			 	 //exit loop if we're at edge of bitmap or color area
				
			}
			lFillLoc++;
		    if (lFillLoc < rect.MinX)
		        rect.MinX = lFillLoc;
			
			//FIND RIGHT EDGE OF COLOR AREA
			var rFillLoc=x; //the location to check/fill on the left
			ptr=p;
			while(true)
			{
				ptr[0]=color; //fill with the color
				pixelsChecked[rFillLoc,y]=true;
				rFillLoc++; 		 //increment counter
				ptr+=1;				 //increment pointer
				if(rFillLoc>=bmpsize.Width || !CheckPixel((byte*)ptr,startcolor) ||  (pixelsChecked[rFillLoc,y]))
					break;			 //exit loop if we're at edge of bitmap or color area
				
			}
			rFillLoc--;
		    if (rFillLoc > rect.MaxX)
		        rect.MaxX = rFillLoc;

		    if (y < rect.MinY)
		        rect.MinY = y;
		    if (y > rect.MaxY)
		        rect.MaxY = y;
			
			
			//START THE LOOP UPWARDS AND DOWNWARDS			
			ptr=(int*)(scan0+CoordsToIndex(lFillLoc,y,stride));
			for(var i=lFillLoc;i<=rFillLoc;i++)
			{
				//START LOOP UPWARDS
				//if we're not above the top of the bitmap and the pixel above this one is within the color tolerance
				if(y>0 && CheckPixel(scan0+CoordsToIndex(i,y-1,stride),startcolor) && (!(pixelsChecked[i,y-1])))
					LinearFloodFill4(scan0, i,y-1,bmpsize,stride,startcolor, color, pixelsChecked, ref rect);
				//START LOOP DOWNWARDS
				if(y<(bmpsize.Height-1) && CheckPixel(scan0+CoordsToIndex(i,y+1,stride),startcolor) && (!(pixelsChecked[i,y+1])))
					LinearFloodFill4(scan0, i,y+1,bmpsize,stride,startcolor, color, pixelsChecked, ref rect);
				ptr+=1;
			}
		}
		
		//*********
		//HELPER FUNCTIONS
		//*********

	    ///<summary>Sees if a pixel is within the color tolerance range.</summary>
	    //px - a pointer to the pixel to check
	    //startcolor - a pointer to the color of the pixel we started at
	    private static unsafe bool CheckPixel(byte* px, byte* startcolor)
	    {
	        bool ret = true;
	        for (byte i = 0; i < 3; i++)
	            ret &= (px[i] >= (startcolor[i])) && px[i] <= (startcolor[i]);
	        return ret;
	    }

	    ///<summary>Given X and Y coordinates and the bitmap's stride, returns a pointer offset</summary>
		private static int CoordsToIndex(int x, int y, int stride)
		{
			return (stride*y)+(x*4);
		}

        private static int ToBgra(Color color)
        {
            return color.B + (color.G << 8) + (color.R << 16) + (color.A << 24);
        }
    }
}
