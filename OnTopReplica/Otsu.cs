/*
 * by Tolga Birdal
 * 
*/

using System.Drawing;
using System.Drawing.Imaging;

namespace OnTopReplica
{
    public class Otsu
    {
        // function is used to compute the q values in the equation
        private static float Px(int init, int end, int[] hist)
        {
            int sum = 0;
            int i;
            for (i = init; i <= end; i++)
                sum += hist[i];

            return sum;
        }

        // function is used to compute the mean values in the equation (mu)
        private static float Mx(int init, int end, int[] hist)
        {
            int sum = 0;
            int i;
            for (i = init; i <= end; i++)
                sum += i * hist[i];

            return sum;
        }

        // finds the maximum element in a vector
        private static int FindMax(float[] vec, int n)
        {
            float maxVec = 0;
            int idx=0;
            int i;

            for (i = 1; i < n - 1; i++)
            {
                if (vec[i] > maxVec)
                {
                    maxVec = vec[i];
                    idx = i;
                }
            }
            return idx;
        }

        // simply computes the image histogram
        private static unsafe void GetHistogram(byte* p, int w, int h, int ws, int[] hist)
        {
            hist.Initialize();
            for (int i = 0; i < h; i++)
            {
                for (int j = 0; j < w*3; j+=3)
                {
                    int index=i*ws+j;
                    hist[p[index]]++;
                }
            }
        }

        // find otsu threshold
        private static int GetOtsuThreshold(Bitmap bmp)
        {
            float[] vet=new float[256];
            int[] hist=new int[256];
            vet.Initialize();

            int k;

            BitmapData bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)bmData.Scan0.ToPointer();

                GetHistogram(p,bmp.Width,bmp.Height,bmData.Stride, hist);

                // loop through all possible t values and maximize between class variance
                for (k = 1; k != 255; k++)
                {
                    var p1 = Px(0, k, hist);
                    var p2 = Px(k + 1, 255, hist);
                    var p12 = p1 * p2;
                    if (p12 == 0) 
                        p12 = 1;
                    float diff=(Mx(0, k, hist) * p2) - (Mx(k + 1, 255, hist) * p1);
                    vet[k] = diff * diff / p12;
                    //vet[k] = (float)Math.Pow((Mx(0, k, hist) * p2) - (Mx(k + 1, 255, hist) * p1), 2) / p12;
                }
            }
            bmp.UnlockBits(bmData);

            return (byte)FindMax(vet, 256);
        }

        // simple routine to convert to gray scale
        private static void Convert2GrayScaleFast(Bitmap bmp)
        {
            var bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)bmData.Scan0.ToPointer();
                int stopAddress = (int)p + bmData.Stride * bmData.Height;
                while ((int)p != stopAddress)
                {
                    p[0] = (byte)(.299 * p[2] + .587 * p[1] + .114 * p[0]);
                    p[1] = p[0];
                    p[2] = p[0];
                    p += 3;
                }
            }
            bmp.UnlockBits(bmData);
        }

        // simple routine for thresholdin
        private static void DoThresholding(Bitmap bmp, int threshold)
        {
            var bmData = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            unsafe
            {
                byte* p = (byte*)bmData.Scan0.ToPointer();
                int h= bmp.Height;
                int w = bmp.Width;
                int ws = bmData.Stride;

                for (int i = 0; i < h; i++)
                {
                    byte *row=&p[i*ws];
                    for (int j = 0; j < w * 3; j += 3)
                    {
                        row[j] = (byte)((row[j] > (byte)threshold) ? 255 : 0);
                        row[j+1] = (byte)((row[j+1] > (byte)threshold) ? 255 : 0);
                        row[j + 2] = (byte)((row[j + 2] > (byte)threshold) ? 255 : 0);
                    }
                }
            }
            bmp.UnlockBits(bmData);
        }

        public static void Process(Bitmap bmp, int threshold = -1)
        {
            Convert2GrayScaleFast(bmp);
            if (threshold < 0)
            {
                threshold = GetOtsuThreshold(bmp);
            }
            DoThresholding(bmp, threshold);
        }

    }
}

