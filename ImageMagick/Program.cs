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
            //args = new string[] { @"C:\Users\htc\Desktop\фотографии участков и домов", @"C:\Users\htc\Desktop\test", "resizefolder", "1600", "1600", "261", "174" };

            OpenCL.IsEnabled = false;
            string inFileOrFolder = args[0];
            string outFileOrFolder = args[1];
            string cmd = args[2].ToLower().Trim();

            #region resizefolder
            if (cmd == "resizefolder")
            {
                Directory.CreateDirectory(outFileOrFolder);
                Directory.CreateDirectory($"{outFileOrFolder}/small");
                int width = int.Parse(args[3]), height = int.Parse(args[4]), widtSmallh = int.Parse(args[5]), heightSmall = int.Parse(args[6]);
                List<ResizeFolderModel> md = new List<ResizeFolderModel>();

                Parallel.ForEach(Directory.GetFiles(inFileOrFolder), new ParallelOptions() { MaxDegreeOfParallelism = 8 }, inFile => 
                {
                    string outFile = $"{outFileOrFolder}/{Transliteration.Translit(Path.GetFileName(inFile))}";
                    string outFileSmall = $"{outFileOrFolder}/small/{Transliteration.Translit(Path.GetFileName(inFile))}";

                    using (MagickImage image = new MagickImage(inFile))
                    {
                        #region Локальная функция - "GetModelOfExistingFile"
                        ResizeFolderModel GetModelOfExistingFile(string path)
                        {
                            using (MagickImage img = new MagickImage(path))
                            {
                                return new ResizeFolderModel()
                                {
                                    path = path.Replace($"{outFileOrFolder}/", ""),
                                    width = img.Width,
                                    height = img.Height
                                };
                            }
                        }
                        #endregion

                        #region Уменьшаем изображения
                        // Изображение уже есть
                        // Размер изображения совпадает по высоте или ширине
                        if (File.Exists(outFile) && GetModelOfExistingFile(outFile) is ResizeFolderModel existImg && (width == existImg.width || height == existImg.height))
                        {
                            md.Add(GetModelOfExistingFile(outFile));
                        }
                        else
                        {
                            image.Resize(width, height);
                            image.Write(outFile);

                            // Обновляем модель
                            md.Add(new ResizeFolderModel()
                            {
                                path = outFile.Replace($"{outFileOrFolder}/", ""),
                                width = image.Width,
                                height = image.Height
                            });
                        }
                        #endregion

                        #region Привью
                        // Привью уже есть
                        // Размер привью совпадает по высоте и ширине
                        if (File.Exists(outFileSmall) && GetModelOfExistingFile(outFileSmall) is ResizeFolderModel existSmallImg && widtSmallh == existSmallImg.width && heightSmall == existSmallImg.height)
                        {
                            md.Add(GetModelOfExistingFile(outFileSmall));
                        }
                        else
                        {
                            if (heightSmall > widtSmallh)
                                image.Resize(0, heightSmall);
                            else
                                image.Resize(widtSmallh, 0);

                            image.Crop(0, 0, widtSmallh, heightSmall);
                            image.Write(outFileSmall);

                            // Обновляем модель
                            md.Add(new ResizeFolderModel()
                            {
                                path = outFileSmall.Replace($"{outFileOrFolder}/", ""),
                                width = image.Width,
                                height = image.Height
                            });
                        }
                        #endregion
                    }
                });

                Console.WriteLine(JsonConvert.SerializeObject(md));
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
                    image.Write(outFileOrFolder);
                }

                Console.WriteLine("{\"success\":true}");
            }
        }
    }
}
