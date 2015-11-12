﻿using System;
using System.Linq;
using FarseerPhysics.Common;
using FarseerPhysics.Dynamics;
using FarseerPhysics.Factories;
using GameProject.Entities;
using GameProject.Entities.Bullets;
using GameProject.Views;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGameProxies;
using XTiled;

namespace GameProject
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : Game
    {
        //private readonly GraphicsDeviceManager _graphics;
        //private SpriteBatch _spriteBatch;
        private IGameState _gameState;
        private World _world;

        public Game1()
        {
            Content.RootDirectory = "Content";
            new GraphicsDeviceManager(this)
            {
                PreferredBackBufferWidth = 1024,
                PreferredBackBufferHeight = 640,
                IsFullScreen = true
            };
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            base.Initialize();
            IsMouseVisible = true;

            var worldSpriteBatch = new SpriteBatchProxy(new SpriteBatch(GraphicsDevice));
            var hudSpriteBatch = new SpriteBatchProxy(new SpriteBatch(GraphicsDevice));
            _world = new World(Vector2.Zero);
            var physicsUpdater = new WorldUpdateableAdapter(_world);
            var controllersProcessor = new EntityProcessor<IController>();
            var controllersUpdater = new EntityUpdater<IController>(controllersProcessor);
            var modelsProcessor = new EntityProcessor<IModel>();
            var modelsUpdater = new EntityUpdater<IModel>(modelsProcessor);
            var viewsProcessor = new EntityProcessor<IView>();
            var viewsUpdater = new EntityUpdater<IView>(viewsProcessor);

            var worldMapViewProcessor = new EntityProcessor<IView>();
            var worldCharacterViewProcessor = new EntityProcessor<IView>();

            var worldViewCamera = new Camera(worldSpriteBatch) { ViewProcessors = new[] { worldMapViewProcessor, worldCharacterViewProcessor } };
            var hudViewCamera = new Camera(hudSpriteBatch);
            var updateStepMultiplexer = new UpdationMultiplexer { Updateables = { controllersUpdater, modelsUpdater, physicsUpdater, } };
            var drawMultiplexer = new DrawingMultiplexer { Drawables = { worldViewCamera, hudViewCamera } };
            _gameState = new GameStateAdapter { UpdateStepUpdateable = updateStepMultiplexer, DrawStepUpdateable = viewsUpdater, Drawable = drawMultiplexer };

            var map = Content.Load<Map>("01");
            //добавить мапу как вьюху
            var mapView = new MapView(map);
            worldMapViewProcessor.Collect(mapView);

            //добавить стены в мир
            var wallBody = new Body(_world, Vector2.Zero);
            var verticesArrays = map.ObjectLayers["walls"].MapObjects
                .Select(mapObject => new Vertices(mapObject.Polyline.Points.Select(p => new Vector2(p.X/10f, p.Y/10f))));
            foreach (var vertices in verticesArrays)
                FixtureFactory.AttachChainShape(vertices, wallBody);

            //добавить танки как полноценные тройки MVC
            var bodyTexture = Content.Load<Texture2D>("body");
            var towerTexture = Content.Load<Texture2D>("tower");
            var bulletTexture = Content.Load<Texture2D>("bullet");

            var bulletModelFactory = new BulletModelFactory(_world);
            var bulletViewFactory = new BulletViewFactory(bulletTexture);
            var bulletSpawner = new BulletSpawner(modelsProcessor, viewsProcessor, worldCharacterViewProcessor, bulletModelFactory, bulletViewFactory);

            var tankModelFactory = new TankModelFactory(_world, bulletSpawner);
            var tankViewFactory = new TankViewFactory(bodyTexture, towerTexture);
            var tankUserControllerFactory = new TankUserControllerFactory();
            var tankEnemyControllerFactory = new TankEnemyControllerFactory();


            var userTankSpawner = new TankSpawner(modelsProcessor, controllersProcessor, viewsProcessor, worldCharacterViewProcessor, tankModelFactory, tankViewFactory, tankUserControllerFactory);
            var enemyTankSpawner = new TankSpawner(modelsProcessor, controllersProcessor, viewsProcessor, worldCharacterViewProcessor, tankModelFactory, tankViewFactory, tankEnemyControllerFactory);

            foreach (var mapObject in map.ObjectLayers["entities"].MapObjects)
            {
                var position = new Vector2(mapObject.Bounds.X/10f, mapObject.Bounds.Y/10f);
                var rotation = 0;
                switch (mapObject.Type)
                {
                    case "Player":
                        userTankSpawner.Spawn(position, rotation);
                        break;
                    case "Enemy":
                        enemyTankSpawner.Spawn(position, rotation);
                        break;
                }
            }
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
        }


        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            _gameState.Update(gameTime);
            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);
            _gameState.Draw(gameTime);
            base.Draw(gameTime);
        }
    }

    public class BulletViewFactory : IBulletViewFactory
    {
        private readonly Texture2D _bulletTexture;

        public BulletViewFactory(Texture2D bulletTexture)
        {
            if (bulletTexture == null)
                throw new ArgumentNullException("bulletTexture");
            _bulletTexture = bulletTexture;
        }

        public IView Create(IBullet bullet)
        {
            return new BulletView(bullet, _bulletTexture);
        }
    }

    public interface IBulletViewFactory
    {
        IView Create(IBullet model);
    }

    public class BulletModelFactory : IBulletModelFactory
    {
        private readonly World _world;

        public BulletModelFactory(World world)
        {
            if (world == null)
                throw new ArgumentNullException("world");
            _world = world;
        }

        public IBullet Create(Vector2 position, float rotation, ITank ownerTank)
        {
            return new SimpleBullet(_world, position, rotation, ownerTank);
        }
    }

    public interface IBulletModelFactory
    {
        IBullet Create(Vector2 position, float rotation, ITank ownerTank);
    }
}
