using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Souvenir
{
    public static class Sprites
    {
        private static readonly Dictionary<string, Texture2D> _circleSpriteCache = new();
        private static readonly Dictionary<string, Texture2D> _gridSpriteCache = new();
        private static readonly Dictionary<AudioClip, Texture2D> _audioSpriteCache = new();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="souvenirModule"></param>
        /// <param name="width">how many circles will appear in each row</param>
        /// <param name="height">how many circles will appear in each column</param>
        /// <param name="radius">the radius of each circle</param>
        /// <param name="litdots">A binary number that shows which does will be visible</param>
        /// <param name="outline">If circle that are not visuble should have an outline</param>
        /// <returns></returns>
        public static Sprite GetCircleAnswer(SouvenirModule souvenirModule, int width, int height, int litdots, int radius, bool outline = false)
        {
            //Currently an exception is being thrown somewhere. I assume when 'binary' called, but I have not verified

            //the binary reads from left to right of each circle
            //Ex: If the width and height are both 2:
            //1 will be the top left circle
            //10 will be the top right circle
            //101 will be the top right and top left circles
            var binary = Convert.ToString(litdots, 2);
            var gap = 10; // how many pixels will between each circle vertically and horizontally
            var textureWidth =  width * radius * 2 + ((width - 1) * gap);
            var textureHeight = height * radius * 2 + ((height - 1) * gap);
            var pixelCount = textureWidth * textureHeight;
            var key = $"{width}:{height}:{litdots}:{outline}";

            //if the sprite is not cached, create it
            if (!_circleSpriteCache.TryGetValue(key, out var tx))
            {
                //create the base of the texture
                tx = new Texture2D(textureWidth, textureHeight, TextureFormat.ARGB32, false);
                _circleSpriteCache.Add(key, tx);

                var pixels = Ut.NewArray(pixelCount, _ => new Color32(0x00, 0x00, 0x00, 0x00));
                
                //Get the list of center poitns for each circle
                //the first circle (top left)'s position will be the top left's position offsetted by the radius (plus for the x axis and minus for the y axis)
                Vector2 topLeftCirclePos = new Vector2(radius, textureWidth - 1 - radius);

                List<Vector2> circleCenters = new List<Vector2>() { topLeftCirclePos };
                for (int column = 0; column < width; column++)
                {
                    for (int row = 0; row < height; row++)
                    {
                        int xoffset = (radius * 2 + gap) * column;
                        int yoffset = -(radius * 2 + gap) * row;

                        //get the center position for this circle
                        circleCenters.Add(topLeftCirclePos + new Vector2(xoffset, yoffset));
                    }
                }

                //for each pixel, figure out if the the pixel should be white or stay clear
                for (int pixelIndex = 0; pixelIndex < pixelCount; pixelIndex++)
                {
                    //a pixel should be white if it intersects or is on the edge of a circle
                    //this can be calculated by check if the distance between the pixel and the center position of the circle
                    //is less than or equal to the radius
                    foreach (Vector2 centerPos in circleCenters)
                    {
                        //if this circle shouldn't be drawn, move on
                        if (binary[pixelIndex] == '0' && !outline)
                            continue;

                        Vector2 pixelPosition = new Vector2(pixelIndex % width, pixelIndex / width);
                        float distanceSquared = Mathf.Pow(pixelPosition.x - centerPos.x, 2) + Mathf.Pow(pixelPosition.y - centerPos.y, 2);
                        float radiusSquared = Mathf.Pow(radius, 2);

                        //check if this dot falls in a circle that should just have its border drawn
                        //check if this dots falls in a circle that should be filled in
                        if ((binary[pixelIndex] == '0' && distanceSquared == radiusSquared) || 
                            (binary[pixelIndex] == '1' && distanceSquared <= radiusSquared))
                        {
                            //set the pixel to white
                            pixels[pixelIndex] = new Color32(0xFF, 0xF8, 0xDD, 0xFF);
                        }
                    }
                }
            }

            var sprite = Sprite.Create(tx, new Rect(0, 0, textureWidth, textureHeight), new Vector2(0, .5f), textureHeight * (60f / 17));
            sprite.name = key;

            return sprite;
        }

        public static Sprite GenerateGridSprite(Coord coord, float size = 1f)
        {
            var tw = 4 * coord.Width + 1;
            var th = 4 * coord.Height + 1;
            var key = $"{coord.Width}:{coord.Height}:{coord.Index}";
            if (!_gridSpriteCache.TryGetValue(key, out var tx))
            {
                tx = new Texture2D(tw, th, TextureFormat.ARGB32, false);
                tx.SetPixels32(Ut.NewArray(tw * th, ix =>
                    (ix % tw) % 4 == 0 || (ix / tw) % 4 == 0 ? new Color32(0xFF, 0xF8, 0xDD, 0xFF) :
                    (ix % tw) / 4 + coord.Width * (coord.Height - 1 - (ix / tw / 4)) == coord.Index ? new Color32(0xD8, 0x40, 0x00, 0xFF) : new Color32(0xFF, 0xF8, 0xDD, 0x00)));
                tx.Apply();
                tx.wrapMode = TextureWrapMode.Clamp;
                tx.filterMode = FilterMode.Point;
                _gridSpriteCache.Add(key, tx);
            }
            var sprite = Sprite.Create(tx, new Rect(0, 0, tw, th), new Vector2(0, .5f), th * (60f / 17) / size);
            sprite.name = coord.ToString();
            return sprite;
        }

        public static Sprite GenerateGridSprite(int width, int height, int index, float size = 1f)
        {
            return GenerateGridSprite(new Coord(width, height, index), size);
        }

        public static Sprite GenerateGridSprite(string spriteKey, int tw, int th, (int x, int y)[] squares, int highlightedCell, string spriteName, float? pixelsPerUnit = null)
        {
            var key = $"{spriteKey}:{highlightedCell}";
            if (!_gridSpriteCache.TryGetValue(key, out var tx))
            {
                tx = new Texture2D(tw, th, TextureFormat.ARGB32, false);
                var pixels = Ut.NewArray(tw * th, _ => new Color32(0xFF, 0xF8, 0xDD, 0x00));
                for (var sqIx = 0; sqIx < squares.Length; sqIx++)
                {
                    var (x, y) = squares[sqIx];
                    for (var i = 0; i <= 4; i++)
                        pixels[x + i + tw * (th - 1 - y)] = pixels[x + i + tw * (th - 1 - y - 4)] =
                            pixels[x + tw * (th - 1 - y - i)] = pixels[x + 4 + tw * (th - 1 - y - i)] = new Color32(0xFF, 0xF8, 0xDD, 0xFF);
                    if (sqIx == highlightedCell)
                        for (var i = 0; i < 3 * 3; i++)
                            pixels[x + 1 + (i % 3) + tw * (th - y - 2 - (i / 3))] = new Color32(0xD8, 0x40, 0x00, 0xFF);
                }
                tx.SetPixels32(pixels);
                tx.Apply();
                tx.wrapMode = TextureWrapMode.Clamp;
                tx.filterMode = FilterMode.Point;
                _gridSpriteCache.Add(key, tx);
            }
            var sprite = Sprite.Create(tx, new Rect(0, 0, tw, th), new Vector2(0, .5f), pixelsPerUnit ?? th * (60f / 17));
            sprite.name = spriteName;
            return sprite;
        }

        public static Sprite TranslateSprite(this Sprite sprite, float? pixelsPerUnit = null, string name = null)
        {
            var newSprite = Sprite.Create((sprite ?? throw new ArgumentNullException(nameof(sprite))).texture, sprite.rect, new Vector2(0, .5f), pixelsPerUnit ?? sprite.pixelsPerUnit);
            newSprite.name = name ?? sprite.name;
            return newSprite;
        }

        public static IEnumerable<Sprite> TranslateSprites(this IEnumerable<Sprite> sprites, float? pixelsPerUnit) =>
            (sprites ?? throw new ArgumentNullException(nameof(sprites))).Select(spr => TranslateSprite(spr, pixelsPerUnit));

        // Height must be even, should be a power of 2
        const int HEIGHT = 128;
        const int WIDTH = HEIGHT * 4;
        const int MIN_LINE = 3;
        public static Sprite RenderWaveform(AudioClip answer, SouvenirModule module, float multiplier)
        {
            if (!_audioSpriteCache.TryGetValue(answer, out Texture2D tex))
            {
                tex = new(WIDTH, HEIGHT, TextureFormat.RGBA32, false, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
                _audioSpriteCache.Add(answer, tex);

                answer.LoadAudioData();
                Color[] result = Enumerable.Repeat((Color) new Color32(0xFF, 0xF8, 0xDD, 0x00), WIDTH * HEIGHT).ToArray();
                tex.SetPixels(result);
                tex.Apply(false, false);

                if (answer.samples * answer.channels < WIDTH)
                {
                    Debug.Log($"[Souvenir #{module._moduleId}] Warning!: Audio clip too short (minimum data length = {WIDTH}): {answer.name}");
                }
                else
                {
                    Debug.Log($"‹Souvenir #{module._moduleId}› Starting thread to render waveform: {answer.name}");
                    var runner = new GameObject($"Waveform Renderer - {answer.name}", typeof(DataBehaviour));
                    UnityEngine.Object.DontDestroyOnLoad(runner);
                    var behavior = runner.GetComponent<DataBehaviour>();
                    behavior.Result = result;
                    float[] data = new float[answer.samples * answer.channels];
                    answer.GetData(data, 0);

                    new Thread(() => RenderRMS(data, behavior, multiplier))
                    {
                        IsBackground = true,
                        Name = $"Waveform Renderer - {answer.name}"
                    }.Start();
                    behavior.StartCoroutine(CopyData(behavior, tex, runner, answer.name, module._moduleId));
                }
            }

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, WIDTH, HEIGHT), new Vector2(0, 0.5f), WIDTH);
            sprite.name = answer.name;
            return sprite;
        }

        private static IEnumerator CopyData(DataBehaviour behavior, Texture2D tex, GameObject runner, string name, int id)
        {
            while (behavior.FinishedColumns <= WIDTH - 1)
                yield return null;

            tex.SetPixels(behavior.Result);
            tex.Apply(false, true);
            UnityEngine.Object.Destroy(runner);

            Debug.Log($"‹Souvenir #{id}› Finished rendering waveform: {name}");
        }

        private static void RenderRMS(float[] data, DataBehaviour behavior, float multiplier)
        {
            Color32 cream = new(0xFF, 0xF8, 0xDD, 0xFF);
            Color32 black = new(0xFF, 0xF8, 0xDD, 0x00);

            var step = data.Length / WIDTH;
            int start = 0;
            for (int ix = 0; ix < WIDTH; start += step, ix++)
            {
                float totalPlus = 0f, totalMinus = 0f;
                int countPlus = 0, countMinus = 0;
                for (int j = start; j < start + step; j++)
                {
                    if (data[j] > 0f)
                    {
                        totalPlus += data[j] * data[j];
                        countPlus++;
                    }
                    else
                    {
                        totalMinus += data[j] * data[j];
                        countMinus++;
                    }
                }
                var RMSPlus = countPlus == 0 ? 0f : Math.Sqrt(totalPlus / countPlus);
                var RMSMinus = countMinus == 0 ? 0f : Math.Sqrt(totalMinus / countMinus);
                var creamCountPlus = (int) Mathf.Lerp(MIN_LINE, HEIGHT / 2, (float) RMSPlus * multiplier);
                var creamCountMinus = (int) Mathf.Lerp(MIN_LINE, HEIGHT / 2, (float) RMSMinus * multiplier);
                var blackCount = HEIGHT / 2 - creamCountPlus;
                int i = 0;
                for (; i < blackCount; i++)
                    behavior.Result[ix + i * WIDTH] = black;
                for (; i < creamCountPlus + creamCountMinus + blackCount; i++)
                    behavior.Result[ix + i * WIDTH] = cream;
                for (; i < HEIGHT; i++)
                    behavior.Result[ix + i * WIDTH] = black;
                behavior.FinishedColumns++;
            }
        }

        private class DataBehaviour : MonoBehaviour
        {
            public int FinishedColumns = 0;
            public Color[] Result;
        }
    }
}
