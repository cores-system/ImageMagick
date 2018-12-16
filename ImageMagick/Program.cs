using ImageMagick.Models;
using Newtonsoft.Json;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;

namespace ImageMagick
{
    class Program
    {
        static void Main(string[] args)
        {
            //args = new string[] { @"C:\Users\htc\Desktop\screen.png", @"C:\Users\htc\Desktop\2111", "croptotiles", "500", "500", "0", "0" };

            string inFIle = args[0];
            string outFile = args[1];
            string cmd = args[2].ToLower().Trim();

            using (MagickImage image = new MagickImage(inFIle))
            {
                switch (cmd)
                {
                    #region resize
                    case "resize":
                        {
                            int width = int.Parse(args[3]), height = int.Parse(args[4]);
                            image.Resize(width, height);
                            break;
                        }
                    #endregion

                    #region resizeToCanvas
                    case "resizetocanvas":
                        {
                            int width = int.Parse(args[3]), height = int.Parse(args[4]);
                            image.Resize(width, height);

                            using (var src = image.ToBitmap())
                            {
                                using (var bmp = new Bitmap(width, height, PixelFormat.Format32bppPArgb))
                                {
                                    using (var gr = Graphics.FromImage(bmp))
                                    {
                                        gr.DrawImage(src, (width - src.Width) / 2, (height - src.Height) / 2);
                                        gr.DrawImage(src, (width - src.Width) / 2, (height - src.Height) / 2);
                                        bmp.Save(outFile, ImageFormat.Png);
                                    }
                                }
                            }

                            Console.WriteLine("{\"success\":true}");
                            return;
                        }
                    #endregion

                    #region crop
                    case "crop":
                        {
                            int width = int.Parse(args[3]), height = int.Parse(args[4]), x = int.Parse(args[5]), y = int.Parse(args[6]);
                            image.Crop(x, y, width, height);
                            break;
                        }
                    #endregion

                    #region rcrop
                    case "rcrop":
                        {
                            int width = int.Parse(args[3]), height = int.Parse(args[4]);
                            image.Resize(width, 0);
                            image.Crop(0, 0, width, height);
                            break;
                        }
                    #endregion

                    #region CropToTiles
                    case "croptotiles":
                        {
                            CropTileModel md = new CropTileModel();
                            int width = int.Parse(args[3]), height = int.Parse(args[4]);
                            string outFolder = Regex.Replace(args[1], @"(\\|/)$", "") + Path.DirectorySeparatorChar;

                            // Удаляем существующие файлы
                            foreach (var rmFile in Directory.EnumerateFiles(outFolder))
                                File.Delete(rmFile);

                            int widthOfOnePiece = width == 0 ? image.Width : width;
                            int heightOfOnePiece = height == 0 ? image.Height : height;

                            int numColsToCut = (int)Math.Ceiling((double)(image.Width / widthOfOnePiece));
                            int numRowsToCut = (int)Math.Ceiling((double)(image.Height / heightOfOnePiece));

                            for (var x = 0; x <= numColsToCut; ++x)
                            {
                                for (var y = 0; y <= numRowsToCut; ++y)
                                {
                                    using (IMagickImage img = image.Clone())
                                    {
                                        try
                                        {
                                            string imgPath = $@"{outFolder}{x}_{y}_{widthOfOnePiece}_{heightOfOnePiece}.png";
                                            int startX = (int)(x * widthOfOnePiece), startY = (int)(y * heightOfOnePiece);

                                            // Выход за границы
                                            if (startX >= image.Width || startY >= image.Height)
                                                continue;

                                            img.Crop(startX, startY, widthOfOnePiece, heightOfOnePiece);
                                            img.Write(imgPath);

                                            md.images.Add(new ImgCropTileModel()
                                            {
                                                path = Path.GetFileName(imgPath),
                                                x = startX,
                                                y = startY
                                            });
                                        }
                                        catch { }
                                    }
                                }
                            }

                            Console.WriteLine(JsonConvert.SerializeObject(md));
                            return;
                        }
                    #endregion
                }

                // Save
                image.Write(outFile);
            }

            Console.WriteLine("{\"success\":true}");
        }
    }
}
