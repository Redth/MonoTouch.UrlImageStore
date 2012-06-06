using System;
using System.Drawing;
using MonoTouch.Foundation;
using MonoTouch.CoreFoundation;
using MonoTouch.UIKit;

namespace MonoTouch.UrlImageStore
{
	public static class Graphics
	{
		
        // Child proof the image by rounding the edges of the image
		public static UIImage RoundCorners (UIImage image)
        {
			return RoundCorners(image, 4);
		}
		
        public static UIImage RoundCorners (UIImage image, int radius)
        {
			if (image == null)
				throw new ArgumentNullException ("image");
			
			UIImage converted = image;
			
			image.InvokeOnMainThread(() => {
	            UIGraphics.BeginImageContext (image.Size);
				float imgWidth = image.Size.Width;
				float imgHeight = image.Size.Height;
	
	            var c = UIGraphics.GetCurrentContext ();
	
	            c.BeginPath ();
	            c.MoveTo (imgWidth, imgHeight/2);
	            c.AddArcToPoint (imgWidth, imgHeight, imgWidth/2, imgHeight, radius);
	            c.AddArcToPoint (0, imgHeight, 0, imgHeight/2, radius);
	            c.AddArcToPoint (0, 0, imgWidth/2, 0, radius);
	            c.AddArcToPoint (imgWidth, 0, imgWidth, imgHeight/2, radius);
	            c.ClosePath ();
	            c.Clip ();
	
	            image.Draw (new PointF (0, 0));
	            converted = UIGraphics.GetImageFromCurrentImageContext ();
	            UIGraphics.EndImageContext ();
			});
			
            return converted;
        }
		
		public static UIImage Scale(UIImage image, float maxWidthAndHeight)
		{
			//Perform Image manipulation, make the image fit into a 48x48 tile without clipping.  
			
			UIImage scaledImage = image;
			
			image.InvokeOnMainThread(() => {
				float fWidth = image.Size.Width;
				float fHeight = image.Size.Height;
				float fTotal = fWidth>=fHeight?fWidth:fHeight;
				float fDifPercent = maxWidthAndHeight / fTotal;
				float fNewWidth = fWidth*fDifPercent;
				float fNewHeight = fHeight*fDifPercent;
				
				SizeF newSize = new SizeF(fNewWidth,fNewHeight);
				
				UIGraphics.BeginImageContext (newSize);
		        var context = UIGraphics.GetCurrentContext ();
		        context.TranslateCTM (0, newSize.Height);
		        context.ScaleCTM (1f, -1f);
		
		        context.DrawImage (new RectangleF (0, 0, newSize.Width, newSize.Height), image.CGImage);
		
		        scaledImage = UIGraphics.GetImageFromCurrentImageContext();
		        UIGraphics.EndImageContext();
			});
			
			return scaledImage;
		}
	}
}

