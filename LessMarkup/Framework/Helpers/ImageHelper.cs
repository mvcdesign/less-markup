﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at http://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Web;
using LessMarkup.DataObjects.Common;
using LessMarkup.DataObjects.Gallery;
using LessMarkup.Framework.Configuration;
using LessMarkup.Interfaces.Cache;
using LessMarkup.Interfaces.Data;
using LessMarkup.Interfaces.Security;

namespace LessMarkup.Framework.Helpers
{
    public static class ImageHelper
    {
        public static long? SaveGallery(IDomainModel domainModel, long? galleryId, IList<HttpPostedFile> files,
            long[] unchangedImages, ICurrentUser currentUser, IDataCache dataCache)
        {
            var fileCount = files.Count(f => f.ContentLength > 0);

            Gallery gallery;
            if (!galleryId.HasValue)
            {
                if (fileCount == 0)
                {
                    return null;
                }

                gallery = domainModel.GetSiteCollection<Gallery>().Create();
                domainModel.GetSiteCollection<Gallery>().Add(gallery);
            }
            else
            {
                gallery = domainModel.GetSiteCollection<Gallery>().Single(g => g.GalleryId == galleryId);
                var galleryImageCount = 0;
                if (unchangedImages == null || unchangedImages.Length <= 0)
                {
                    if (fileCount == 0)
                    {
                        galleryImageCount = domainModel.GetSiteCollection<GalleryImage>().Count(g => g.GalleryId == galleryId);
                    }
                }
                else
                {
                    var imagesToRemove = new List<GalleryImage>();

                    foreach (var galleryImage in domainModel.GetSiteCollection<GalleryImage>().Where(g => g.GalleryId == galleryId.Value).Include(g => g.Image))
                    {
                        if (!unchangedImages.Contains(galleryImage.ImageId))
                        {
                            imagesToRemove.Add(galleryImage);
                        }
                        else
                        {
                            galleryImageCount++;
                        }
                    }

                    foreach (var galleryImage in imagesToRemove)
                    {
                        domainModel.GetSiteCollection<Image>().Remove(galleryImage.Image);
                        domainModel.GetSiteCollection<GalleryImage>().Remove(galleryImage);
                    }
                }

                if (fileCount == 0)
                {
                    if (galleryImageCount == 0)
                    {
                        domainModel.GetSiteCollection<Gallery>().Remove(gallery);
                        domainModel.SaveChanges();
                        return null;
                    }

                    domainModel.SaveChanges();
                    return galleryId;
                }
            }

            domainModel.SaveChanges();

            foreach (var file in files)
            {
                if (file.ContentLength == 0)
                {
                    continue;
                }

                var galleryImage = domainModel.GetSiteCollection<GalleryImage>().Create();
                galleryImage.GalleryId = gallery.GalleryId;
                galleryImage.ImageId = SaveImage(domainModel, null, file, currentUser, dataCache);

                domainModel.GetSiteCollection<GalleryImage>().Add(galleryImage);
            }

            domainModel.SaveChanges();

            return gallery.GalleryId;
        }

        public static long SaveImage(IDomainModel domainModel, long? imageId, HttpPostedFile file, ICurrentUser currentUser, IDataCache dataCache)
        {
            return SaveImage(domainModel, imageId, file.ContentLength, file.InputStream, file.FileName, currentUser, dataCache);
        }

        public static long SaveImage(IDomainModel domainModel, long? imageId, HttpPostedFileBase file, ICurrentUser currentUser, IDataCache dataCache)
        {
            return SaveImage(domainModel, imageId, file.ContentLength, file.InputStream, file.FileName, currentUser, dataCache);
        }

        public static void DeleteImage(IDomainModel domainModel, long imageId)
        {
            var image = domainModel.GetSiteCollection<Image>().Single(i => i.ImageId == imageId);
            domainModel.GetSiteCollection<Image>().Remove(image);
        }

        public static long SaveImage(IDomainModel domainModel, long? imageId, int contentLength, Stream inputStream, string fileName, ICurrentUser currentUser, IDataCache dataCache)
        {
            var configurationCache = dataCache.Get<SiteConfigurationCache>();

            if (contentLength > configurationCache.MaximumImageSize)
            {
                throw new Exception(string.Format("Image size is bigger than allowed ({0})", configurationCache.MaximumImageSize));
            }

            byte[] imageBytes;
            byte[] thumbnailBytes;
            int imageWidth, imageHeight;

            using (var imageData = System.Drawing.Image.FromStream(inputStream, true, true))
            {
                using (var stream = new MemoryStream())
                {
                    imageData.Save(stream, ImageFormat.Png);
                    imageBytes = stream.ToArray();
                    imageWidth = imageData.Width;
                    imageHeight = imageData.Height;
                }

                using (var thumbnail = imageData.GetThumbnailImage(configurationCache.ThumbnailWidth, configurationCache.ThumbnailHeight, () => false, IntPtr.Zero))
                {
                    using (var stream = new MemoryStream())
                    {
                        thumbnail.Save(stream, ImageFormat.Png);
                        thumbnailBytes = stream.ToArray();
                    }
                }
            }

            Image image = null;

            if (imageId.HasValue)
            {
                image = domainModel.GetSiteCollection<Image>().Single(i => i.ImageId == imageId.Value);
            }

            if (image == null)
            {
                image = domainModel.GetSiteCollection<Image>().Create();
                domainModel.GetSiteCollection<Image>().Add(image);
                image.Created = DateTime.UtcNow;
            }
            else
            {
                image.Updated = DateTime.UtcNow;
            }

            image.Width = imageWidth;
            image.Height = imageHeight;
            image.FileName = fileName;
            image.ImageType = ImageType.Png;
            image.UserId = currentUser.UserId;
            image.Data = imageBytes;
            image.Thumbnail = thumbnailBytes;

            domainModel.SaveChanges();
 
            return image.ImageId;
        }

        public static void LimitImageSize(long imageId, IDomainModel domainModel, int width, int height)
        {
            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException("width");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException("height");
            }

            var image = domainModel.GetSiteCollection<Image>().Single(i => i.ImageId == imageId);

            if (image.Width <= width && image.Height <= height)
            {
                return;
            }

            var newImageWidth = image.Width;
            var newImageHeight = image.Height;

            if (newImageWidth > width)
            {
                newImageHeight = (int)(newImageHeight * ((double) newImageWidth/image.Width));
                newImageWidth = width;
            }

            if (newImageHeight > height)
            {
                newImageWidth = (int) (newImageWidth*((double) newImageHeight/image.Height));
                newImageHeight = height;
            }

            System.Drawing.Image thumbnail;

            using (var stream = new MemoryStream(image.Data))
            {
                using (var imageData = System.Drawing.Image.FromStream(stream, true, true))
                {
                    thumbnail = imageData.GetThumbnailImage(newImageWidth, newImageHeight, () => false, IntPtr.Zero);
                }
            }

            using (var stream = new MemoryStream())
            {
                thumbnail.Save(stream, ImageFormat.Png);
                image.Data = stream.ToArray();
            }

            image.Width = newImageWidth;
            image.Height = newImageHeight;
            image.ImageType = ImageType.Png;
        }
    }
}
