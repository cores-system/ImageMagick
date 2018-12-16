using System;
using System.Collections.Generic;

namespace ImageMagick.Models
{
    public class CropTileModel
    {
        public List<ImgCropTileModel> images = new List<ImgCropTileModel>();
    }

    public class ImgCropTileModel
    {
        public string path { get; set; }
        public int x { get; set; }
        public int y { get; set; }
    }
}
