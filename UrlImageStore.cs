using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using MonoTouch.CoreFoundation;
using MonoTouch.Foundation;
using MonoTouch.UIKit;

namespace MonoTouch.UrlImageStore
{
	public interface IUrlImageUpdated<TKey>
	{
		void UrlImageUpdated(TKey id);
	}

	public class UrlImageStore<TKey>
	{
		public delegate UIImage ProcessImageDelegate(UIImage img, TKey id);
		static NSOperationQueue opQueue;
		
		readonly static string baseDir;
		
		string picDir;
		
		
		LRUCache<TKey, UIImage> cache;
						
		static UrlImageStore()
		{
			baseDir  = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.MyDocuments));
			
			opQueue = new NSOperationQueue();
			opQueue.MaxConcurrentOperationCount = 4;
		}
		
		public static int MaxConcurrentOperationCount
		{
			get { return opQueue.MaxConcurrentOperationCount; }
			set { opQueue.MaxConcurrentOperationCount = value; }
		}
		
		public UrlImageStore(int capacity, string storeName, ProcessImageDelegate processImage)
		{
			this.Capacity = capacity;
			this.StoreName = storeName;
			this.ProcessImage = processImage;
			
			cache = new LRUCache<TKey, UIImage>(capacity);
			
			if (!Directory.Exists(Path.Combine(baseDir, "Caches/Pictures/")))
				Directory.CreateDirectory(Path.Combine(baseDir, "Caches/Pictures/"));
			
			picDir = Path.Combine(baseDir, "Caches/Pictures/" + storeName);
			
			if (!Directory.Exists(picDir))
				Directory.CreateDirectory(picDir);
			
		}
		
		public void DeleteCachedFiles()
		{
		//string[] files = new string[]{};
			
			try { this.cache.Clear(); }
			catch { }
			
			try 
			{ 
				Directory.Delete(picDir, true);
//				files = Directory.GetFiles(picDir); 
			}
			catch (Exception ex)
			{ 
				Console.WriteLine("Failed to get files: " + ex.ToString());
			}
			
			//foreach (string file in files)
			//{
			//	Console.WriteLine("Deleting: " + file);
			//	
			//	try { File.Delete(file); }
			//	catch (Exception ex)
			//	{ 
			//	Console.WriteLine("Failed to delete: " + file + " -> " + ex.ToString());
			//	}
			//}
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
				{
					//Console.WriteLine("Loading from Memory Cache: " + id);
					return cache[id];
				}
			}
			
			//Next check for a saved file, and load it into cache and return it if found
			string picFile = picDir + "/" + id + ".png";
			if (File.Exists(picFile))
			{
				UIImage img = null;
				
				//Console.WriteLine("Loading from Cache: " + picFile);
				
				try { img = UIImage.FromFileUncached(picFile); }
				catch { }
				
				if (img != null)
				{					
					AddToCache(id, img); //Add it to cache
					return img; //Return this image
				}
			}

			//At this point the file needs to be downloaded, so queue it up to download
			//lock (queue)
		//	{
		//		queue.Enqueue(new UrlImageStoreRequest<TKey>() { Id = id, Url = url, Notify = notify });
		//	}

			//If we haven't maxed out our threads we should start another to download the images
		//	if (threadCount < maxWorkers)
		//		ThreadPool.QueueUserWorkItem(new WaitCallback(DownloadImagesWorker));
		
			//opQueue.AddOperation(new DownloadImageOperation<TKey>(this, id, url, notify));
			
			opQueue.AddOperation(delegate {
				var img = UIImage.LoadFromData(NSData.FromUrl(NSUrl.FromString(url)));
			
				if (this.ProcessImage != null)
	 				img = this.ProcessImage(img, id);
			
				this.AddToCache(id, img);
			
				notify.UrlImageUpdated(id);	
			});
			
			//Return the default while they wait for the queued download
			return this.DefaultImage;
		}

//		void DownloadImagesWorker(object state)
//		{
//			threadCount++;
//
//			UrlImageStoreRequest<TKey> nextReq = null;
//			
//			while ((nextReq = GetNextRequest()) != null)
//			{
//				UIImage img = null;
//
//				
//				try { img = UIImage.LoadFromData(NSData.FromUrl(NSUrl.FromString(nextReq.Url))); }
//				catch (Exception ex) 
//				{
//					Console.WriteLine("Failed to Download Image: " + ex.Message + Environment.NewLine + ex.StackTrace);
//				}
//				
//				if (img == null)
//					continue;
//
//				//See if the consumer needs to do any processing to the image
//				if (this.ProcessImage != null)
//					img = this.ProcessImage(img, nextReq.Id);
//					
//				//Add it to cache
//				AddToCache(nextReq.Id, img);
//
//			
//				
//				//Notify the listener waiting for this,
//				// but do this on the main thread so the user of this class doesn't worry about that
//				//nsDispatcher.BeginInvokeOnMainThread(delegate { nextReq.Notify.UrlImageUpdated(nextReq.Id); });
//				nextReq.Notify.UrlImageUpdated(nextReq.Id);
//			}
//
//			threadCount--;
//		}

		internal void AddToCache(TKey id, UIImage img)
		{
			lock (cache)
			{
				if (cache.ContainsKey(id))
					cache[id] = img;
				else
					cache.Add(id, img);
			}
			
		
			string file = picDir + "/" + id + ".png";
			
			if (!File.Exists(file))
			{
				//Save it to disk
				NSError err = null;
				try 
				{ 
					img.AsPNG().Save(file, false, out err); 
					if (err != null)
						Console.WriteLine(err.Code.ToString() + " - " + err.LocalizedDescription);
					
					//Console.WriteLine("Saved to Cache: " + file);
				}
				catch (Exception ex) 
				{
					Console.WriteLine(ex.Message + Environment.NewLine + ex.StackTrace);
				}
			}
		}
		
//		UrlImageStoreRequest<TKey> GetNextRequest()
//		{
//			UrlImageStoreRequest<TKey> nextReq = null;
//
//			lock (queue)
//			{
//				if (queue.Count > 0)
//					nextReq = queue.Dequeue();
//			}
//
//			return nextReq;
//		}

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
