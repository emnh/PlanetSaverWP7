using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;
using Microsoft.Xna.Framework.Media;

namespace PlanetSaver
{

    class GameConfig
    {
        public static int PlanetCount = 50;
        
        // Performance hack, number of random planets to compute acceleration for per iteration
        // this will make it O(ComputeRandomSubsetSize*n) instead of O(n*n).
        // Doesn't work very well, need to improve.
        public static bool ComputeRandomSubset = false;
        public static int ComputeRandomSubsetSize = 30;

        // planets have random width between min width and max width
        public static int MinWidth = 30;
        public static int MaxWidth = 100;
        // maximum speed
        public static double SpeedCap = 1.0;

        // make bigger planets have greater pull
        public static bool UseWidthAsMass = false;

        // less means stronger gravity. can be negative for inverse gravity
        public static int GravityConstant = 200;
    }

    class PlanetarySystem
    {
        public int PlanetaryWidth { get; set; }
        public int PlanetaryHeight { get; set; }
        public Random _rnd { get; set;  }
        public Planet[] Planets;    

        public PlanetarySystem(Planet[] planets, Random rnd, int planetaryWidth, int planetaryHeight)
        {
            PlanetaryWidth = planetaryWidth;
            PlanetaryHeight = planetaryHeight;
            _rnd = rnd;
            Planets = planets;
        }

        public void Initialize()
        {
            Func<Random, int> randColor = (rnd) => rnd.Next(256);
            foreach (var planet in Planets)
            {
                planet.X = _rnd.Next(0, PlanetaryWidth);
                planet.Y = _rnd.Next(0, PlanetaryHeight);
                planet.XVelocity = _rnd.NextDouble() * 2 - 1;
                planet.YVelocity = _rnd.NextDouble() * 2 - 1;
                planet.Color = new Color(randColor(_rnd), randColor(_rnd), randColor(_rnd));
            }
        }

        public static double Square(double x)
        {
            return x*x;
        }

        public void UpdateVelocities(GameTime gameTime)
        {
            var newVelocities = new Vector2[Planets.Length];
            
            int processedCounter = 0;

            for (int i = 0; i < Planets.Length; i++)
            {
                var p1Index = i;
                if (GameConfig.ComputeRandomSubset)
                {
                    // trick to speed up
                    p1Index = _rnd.Next(Planets.Length);
                    if (processedCounter >= GameConfig.ComputeRandomSubsetSize)
                    {
                        break;
                    }
                    processedCounter++;
                }
                var p1 = Planets[p1Index];
                double newX = p1.XVelocity;
                double newY = p1.YVelocity;
                for (int j = 0; j < Planets.Length; j++)
                {
                    var p2 = Planets[j];

                    if (p1 != p2)
                    {
                        var xDelta = (p2.X - p1.X) / PlanetaryWidth;
                        var yDelta = (p2.Y - p1.Y) / PlanetaryHeight;
                        double distanceSquared = Math.Sqrt(Square(xDelta) + Square(yDelta));
                        double factor = (1 - distanceSquared) * gameTime.ElapsedGameTime.Milliseconds / (GameConfig.GravityConstant * Planets.Length);
                        if (GameConfig.ComputeRandomSubset && GameConfig.ComputeRandomSubsetSize < Planets.Length)
                        {
                            factor *= GameConfig.ComputeRandomSubsetSize;
                        }
                        factor *= p2.Mass;
                        newX += factor * xDelta;
                        newY += factor * yDelta;
                    }
                }
                newVelocities[p1Index] = new Vector2((float) newX, (float) newY);
            }
            for (int i = 0; i < Planets.Length; i++)
            {
                var p = Planets[i];
                if (newVelocities[i] != null)
                {
                    p.XVelocity = newVelocities[i].X;
                    p.YVelocity = newVelocities[i].Y;
                }
            }
        }

        public void UpdateColors(GameTime gameTime)
        {
            foreach (Planet p in Planets)
            {
                Func<int,int> t = (x) => (x + gameTime.ElapsedGameTime.Milliseconds / 10) % 255;
                p.Color = new Color(t(p.Color.R), t(p.Color.G), t(p.Color.B));
            }
        }

        public void UpdatePositions(GameTime gameTime)
        {
            foreach (Planet p in Planets)
            {
                UpdatePosition(p, gameTime);
            }
        }

        public void UpdatePosition(Planet planet, GameTime gameTime)
        {
            double factor = gameTime.ElapsedGameTime.TotalMilliseconds / 5;

            var newValue = planet.X + planet.XVelocity*factor;
            if (newValue > PlanetaryWidth - planet.Width || newValue < 0)
            {
                planet.XVelocity = -planet.XVelocity;
            } else
            {
                planet.X = newValue;
            }

            newValue = planet.Y + planet.YVelocity * factor;
            if (newValue > PlanetaryHeight - planet.Height || newValue < 0)
            {
                planet.YVelocity = -planet.YVelocity;
            }
            else
            {
                planet.Y = newValue;
            }
        }
    }

    class Planet
    {
        public Texture2D Texture { get; private set; }
        public int Width { get; set; }
        public int Height { get; set; }

        public Color Color { get; set; }

        public double Mass { 
            get { return GameConfig.UseWidthAsMass ? Width/(double) GameConfig.MaxWidth : 1; }
        }

        private double _xVelocity;
        private double _yVelocity;

        private static double CapValue(double value, double min, double max)
        {
            return value < min ? min : (value > max ? max : value);
        }

        public double XVelocity {
            get { return _xVelocity; }
            set { _xVelocity = CapValue(value, -GameConfig.SpeedCap, GameConfig.SpeedCap); }
        }
        public double YVelocity
        {
            get { return _yVelocity; }
            set { _yVelocity = CapValue(value, -GameConfig.SpeedCap, GameConfig.SpeedCap); }
        }

        public double X { get; set; }
        public double Y { get; set; }

        public Planet (Random rnd, GraphicsDevice graphicsDevice, int width, int height)
        {
            Texture = new Texture2D(graphicsDevice, width, height, false, SurfaceFormat.Color);
            Width = width;
            Height = height;
            X = 0;
            Y = 0;
            XVelocity = 0;
            YVelocity = 0;
            drawBall(rnd, Texture, width, height);
        }
        
        public static void drawBall(Random rnd, Texture2D texture, int width, int height)
        {
            var pixels = new UInt32[width * height];

            uint maxR = (uint) rnd.Next(0, 256);
            uint maxG = (uint) rnd.Next(0, 256);
            uint maxB = (uint) rnd.Next(0, 256);

            maxR = maxB = maxG = 256;

            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    double dx = (double) x / width * 2 - 1;
                    double dy = (double) y / height * 2 - 1;
                    double dist = 1 - Math.Sqrt(dx * dx + dy * dy); // +(Math.Sin(gameTime.TotalGameTime.TotalSeconds) + 1) / 2;
                    Func<uint, uint> normalize =
                        (max) =>
                            {
                                uint ret = 0;
                                if (dist > 0)
                                {
                                    ret = (uint) (dist*max);
                                }
                                return ret;
                            };
                    var r = normalize(maxR);
                    var g = r;
                    var b = r;
                    var a = 256 - r;
                    //var g = normalize(maxG);
                    //var b = normalize(maxB);
                    pixels[x + y*width] = (a << 24) | (b << 16) | (g << 8) | r; // new Color(0, 0, b).PackedValue; // 0xFF00FF00;
                }
            }

            texture.SetData<UInt32>(pixels, 0, width * height);

        }
    }


    /// <summary>
    /// This is the main type for your game
    /// </summary>
    public class Game1 : Microsoft.Xna.Framework.Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        Rectangle tracedSize;
        private PlanetarySystem _planetarySystem;
        private Planet[] planets;
        private int planetCount = GameConfig.PlanetCount;
        private Random _rnd = new Random();

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";

            // Frame rate is 30 fps by default for Windows Phone.
            TargetElapsedTime = TimeSpan.FromTicks(333333);

            TouchPanel.EnabledGestures = GestureType.Tap;
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            //spriteBatch.GraphicsDevice.Clear(Color.Black);

            planets = new Planet[planetCount];
            _planetarySystem = new PlanetarySystem(planets, _rnd, tracedSize.Width, tracedSize.Height);

            for (int i = 0; i < planetCount; i++)
            {
                int ballWidth = _rnd.Next(GameConfig.MinWidth, GameConfig.MaxWidth);
                int ballHeight = ballWidth;
                planets[i] = new Planet(_rnd, GraphicsDevice, ballWidth, ballHeight);
            }
            _planetarySystem.Initialize();

            //spriteBatch

            // TODO: use this.Content to load your game content here
        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// all content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            // Allows the game to exit
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed)
                this.Exit();

            while (TouchPanel.IsGestureAvailable)
            {
                GestureSample gs = TouchPanel.ReadGesture();
                if (gs.GestureType == GestureType.Tap)
                {
                    //lastGesture = gs;
                    _planetarySystem.Initialize();
                    foreach (var planet in planets)
                    {
                        planet.X = gs.Position.X + _rnd.Next(-100,100);
                        planet.Y = gs.Position.Y + _rnd.Next(-100,100);
                    }
                }
            }

            // Update logic
            _planetarySystem.UpdateVelocities(gameTime);
            _planetarySystem.UpdatePositions(gameTime);
            //_planetarySystem.UpdateColors(gameTime);
            base.Update(gameTime);
        }

        protected override void Initialize()
        {
            tracedSize = GraphicsDevice.PresentationParameters.Bounds;
            //canvas = new Texture2D(GraphicsDevice, tracedSize.Width, tracedSize.Height, false, SurfaceFormat.Color);
            //pixels = new UInt32[tracedSize.Width * tracedSize.Height];

            base.Initialize();
        }

        protected override void Draw(GameTime gameTime)
        {

            //GraphicsDevice.Textures[0] = null;

            //var canvas = new Texture2D(spriteBatch.GraphicsDevice, tracedSize.Width, tracedSize.Height);

            GraphicsDevice.Clear(Color.Black);

            spriteBatch.Begin();

            for (int i = 0; i < planetCount; i++)
            {
                var planet = planets[i];

                /*var r = _rnd.Next(256);
                var g = _rnd.Next(256);
                var b = _rnd.Next(256);
                var color = new Color(r, g, b);
                 * */
                spriteBatch.Draw(planet.Texture, 
                    new Rectangle((int) planet.X, (int) planet.Y, planet.Width, planet.Height),
                    planet.Color);
            }

            spriteBatch.End();
            
            base.Draw(gameTime);
        }
    }
}
