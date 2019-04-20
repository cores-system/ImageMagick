using ImageMagick.Engine;
using ImageMagick.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ImageMagick
{
    /// <summary>
    /// https://github.com/dlemstra/Magick.NET
    /// </summary>
    class Program
    {
        static void Main(string[] args)
        {
            //args = new string[] { @"C:\Users\htc\Desktop\дом", @"C:\Users\htc\Desktop\test", "resizefolder", "W3siZm9sZGVyX25hbWUiOiJtZWRpdW0iLCJ3aWR0aCI6NTAwLCJoZWlnaHQiOjMwMCwiY3JvcGVkIjoiY292ZXIifV0=" };

            OpenCL.IsEnabled = false;
            string inFileOrFolder = PathToUnix(args[0]);
            string outFileOrFolder = PathToUnix(args[1]);
            string cmd = args[2].ToLower().Trim();

            #region resizefolder
            if (cmd == "resizefolder")
            {
                foreach (var md in JsonConvert.DeserializeObject<List<ResizeFolderModel>>(Base64.Decode(args[3])))
                {
                    Parallel.ForEach(Directory.GetFiles(inFileOrFolder, "*.*", SearchOption.AllDirectories), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, inFile =>
                    {
                        string outFolder = PathToUnix(Path.GetDirectoryName(PathToUnix(inFile).Replace(inFileOrFolder, $"{outFileOrFolder}/{md.folder_name}")));
                        Directory.CreateDirectory(outFolder);
                        string outFile = $"{outFolder}/{Transliteration.Translit(Path.GetFileName(inFile))}";

                        using (MagickImage image = new MagickImage(inFile))
                        {
                            #region Локальная функция - "GetModelOfExistingFile"
                            ResizeFolderModel GetModelOfExistingFile(string path)
                                {
                                    using (MagickImage img = new MagickImage(path))
                                    {
                                        return new ResizeFolderModel()
                                        {
                                            width = img.Width,
                                            height = img.Height
                                        };
                                    }
                                }
                            #endregion

                            if (md.croped == "cover")
                            {
                                // Привью нету
                                // Размер привью не совпадает по высоте или ширине
                                if (!File.Exists(outFile) || (GetModelOfExistingFile(outFile) is ResizeFolderModel existSmallImg && (md.width != existSmallImg.width || md.height != existSmallImg.height)))
                                {
                                    // Размер изображения оригинал
                                    float image_w = image.Width;
                                    float image_h = image.Height;

                                    // Какой нужен
                                    float resize_w = md.width;
                                    float resize_h = md.height;

                                    // 
                                    float ratio_w = resize_w / image_w;
                                    float ratio_h = resize_h / image_h;
                                    float ratio = Math.Max(ratio_w, ratio_h);

                                    // Новый размер изображения
                                    int new_width = (int)Math.Ceiling(image_w * ratio);
                                    int new_height = (int)Math.Ceiling(image_h * ratio);

                                    // Куда двигать, да бы было по центру
                                    int offset_left = (int)Math.Ceiling((new_width - resize_w) / 2);
                                    int offset_top = (int)Math.Ceiling((new_height - resize_h) / 2);

                                    // Сохраняем изображение
                                    image.Resize(new_width, new_height);
                                    image.Crop(offset_left, offset_top, (int)resize_w, (int)resize_h);
                                    image.Write(outFile);
                                }
                            }
                            else
                            {
                                // Изображение нету
                                // Размер изображения не совпадает по высоте и ширине
                                if (!File.Exists(outFile) || (GetModelOfExistingFile(outFile) is ResizeFolderModel existImg && (md.width != existImg.width && md.height != existImg.height)))
                                {
                                    image.Resize(md.width, md.height);
                                    image.Write(outFile);
                                }
                            }
                        }
                    });
                }
            }
            #endregion

            else
            {
                using (MagickImage image = new MagickImage(inFileOrFolder))
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
                                            bmp.Save(outFileOrFolder, ImageFormat.Png);
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
                                string outFolder = PathToUnix(Regex.Replace(args[1], @"(\\|/)$", "/"));

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
                    image.Write(outFileOrFolder);
                }
            }

            // Default
            Console.WriteLine("{\"success\":true}");
        }


        static string PathToUnix(string s)
        {
            return Regex.Replace(s, "\\\\", "/");
        }
    }
}
