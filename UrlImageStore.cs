using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MonoTouch.CoreFoundation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace MonoTouch.Dialog.UrlImageStore
{
	public interface IUrlImageUpdated<TKey>
	{
		void UrlImageUpdated(TKey id);
	}

	public class UrlImageStore<TKey>
	{
		public delegate UIImage ProcessImageDelegate(UIImage img, TKey id);
		
		readonly static string baseDir = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.Personal), "..");
		//readonly static string baseDir = Environment.GetFolderPath (Environment.SpecialFolder.Personal).Replace("/Library", "/Documents");
		string picDir;
		
		const int maxWorkers = 4;
		int threadCount = 0;

		LRUCache<TKey, UIImage> cache;
		Queue<UrlImageStoreRequest<TKey>> queue;
		//NSString nsDispatcher = new NSString("x");
	
				
		public UrlImageStore(int capacity, string storeName, ProcessImageDelegate processImage)
		{
			this.Capacity = capacity;
			this.StoreName = storeName;
			this.ProcessImage = processImage;
			
			cache = new LRUCache<TKey, UIImage>(capacity);
			queue = new Queue<UrlImageStoreRequest<TKey>>();
		
			if (!Directory.Exists(Path.Combine(baseDir, "Library/Caches/Pictures/")))
				Directory.CreateDirectory(Path.Combine(baseDir, "Library/Caches/Pictures/"));
			
			picDir = Path.Combine(baseDir, "Library/Caches/Pictures/" + storeName);
			
		}
		
		public void DeleteCachedFiles()
		{
			string[] files = new string[]{};
			
			try { files = Directory.GetFiles(picDir); }
			catch { }
			
			foreach (string file in files)
			{
				try { File.Delete(file); }
				catch { }
			}
		}

		public ProcessImageDelegate ProcessImage
		{
			get;
			private set;
		}
		
		public int Capacity
		{
			get;
			private set;
		}

		public UIImage DefaultImage
		{
			get;
			set;
		}
		
		public string StoreName
		{
			get;
			private set;	
		}

		public UIImage GetImage(TKey id)
		{
			UIImage result = this.DefaultImage;

			lock (cache)
			{
				if (cache.ContainsKey(id))
					result = cache[id];
			}

			return result;
		}
		
		public bool Exists(TKey id)
		{
			lock (cache)
			{
				return cache.ContainsKey(id);	
			}
		}

		public UIImage RequestImage(TKey id, string url, IUrlImageUpdated<TKey> notify)
		{
			//First see if the image is in memory cache already and return it if so
			lock (cache)
			{		
				if (cache.ContainsKey(id))
					return cache[id];
			}
			
			//Next check for a saved file, and load it into cache and return it if found
			string picFile = picDir + id + ".png";
			if (File.Exists(picFile))
			{
				UIImage img = null;
				
				try { img = UIImage.FromFileUncached(picFile); }
				catch { }
				
				if (img != null)
				{					
					AddToCache(id, img); //Add it to cache
					return img; //Return this image
				}
			}

			//At this point the file needs to be downloaded, so queue it up to download
			lock (queue)
			{
				queue.Enqueue(new UrlImageStoreRequest<TKey>() { Id = id, Url = url, Notify = notify });
			}

			//If we haven't maxed out our threads we should start another to download the images
			if (threadCount < maxWorkers)
				ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadImagesWorker));
			
			//Return the default while they wait for the queued download
			return this.DefaultImage;
		}

		void DownloadImagesWorker(object state)
		{
			threadCount++;

			UrlImageStoreRequest<TKey> nextReq = null;
			
			while ((nextReq = GetNextRequest()) != null)
			{
				UIImage img = null;

				try { img = UIImage.LoadFromData(NSData.FromUrl(NSUrl.FromString(nextReq.Url))); }
				catch { }
				
				if (img == null)
					continue;

				//See if the consumer needs to do any processing to the image
				if (this.ProcessImage != null)
					img = this.ProcessImage(img, nextReq.Id);
					
				//Add it to cache
				AddToCache(nextReq.Id, img);

				//Save it to disk
				NSError err = null;
				try { img.AsPNG().Save(picDir + nextReq.Id + ".png", false, out err); }
				catch { }
				
				//Notify the listener waiting for this,
				// but do this on the main thread so the user of this class doesn't worry about that
				//nsDispatcher.BeginInvokeOnMainThread(delegate { nextReq.Notify.UrlImageUpdated(nextReq.Id); });
				nextReq.Notify.UrlImageUpdated(nextReq.Id);
			}

			threadCount--;
		}

		void AddToCache(TKey id, UIImage img)
		{
			lock (cache)
			{
				if (cache.ContainsKey(id))
					cache[id] = img;
				else
					cache.Add(id, img);
			}
		}
		
		UrlImageStoreRequest<TKey> GetNextRequest()
		{
			UrlImageStoreRequest<TKey> nextReq = null;

			lock (queue)
			{
				if (queue.Count > 0)
					nextReq = queue.Dequeue();
			}

			return nextReq;
		}

		public void ReclaimMemory()
		{
			lock (cache)
			{
				cache.ReclaimLRU(cache.Count / 4);
			}
		}
	}

	public class UrlImageStoreRequest<TKey>
	{
		public TKey Id
		{
			get;
			set;
		}

		public string Url
		{
			get;
			set;
		}

		public IUrlImageUpdated<TKey> Notify
		{
			get;
			set;
		}
	}
}
