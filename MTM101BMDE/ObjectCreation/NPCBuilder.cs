﻿using BepInEx;
using HarmonyLib;
using MTM101BaldAPI.Registers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

namespace MTM101BaldAPI.ObjectCreation
{
    public class NPCBuilder<T> where T : NPC
    {
        PluginInfo info;
        public NPCBuilder(PluginInfo info)
        {
            this.info = info;
        }


        readonly static FieldInfo _character = AccessTools.Field(typeof(NPC), "character");
        readonly static FieldInfo _ignorePlayerOnSpawn = AccessTools.Field(typeof(NPC), "ignorePlayerOnSpawn");
        readonly static FieldInfo _ignoreBelts = AccessTools.Field(typeof(NPC), "ignoreBelts");
        readonly static FieldInfo _navigator = AccessTools.Field(typeof(NPC), "navigator");
        readonly static FieldInfo _iEntityTrigger = AccessTools.Field(typeof(Entity), "iEntityTrigger");
        readonly static FieldInfo _minDistance = AccessTools.Field(typeof(PropagatedAudioManager), "minDistance");
        readonly static FieldInfo _maxDistance = AccessTools.Field(typeof(PropagatedAudioManager), "maxDistance");
        readonly static FieldInfo _poster = AccessTools.Field(typeof(NPC), "poster");
        readonly static FieldInfo _entity = AccessTools.Field(typeof(Navigator), "entity");
        readonly static FieldInfo _collider = AccessTools.Field(typeof(Navigator), "collider");
        readonly static FieldInfo _npc = AccessTools.Field(typeof(Looker), "npc");
        readonly static FieldInfo _useHeatMap = AccessTools.Field(typeof(Navigator), "useHeatMap");
        readonly static FieldInfo _hasFov = AccessTools.Field(typeof(Looker), "hasFov");
        readonly static FieldInfo _fieldOfView = AccessTools.Field(typeof(Looker), "fieldOfView");
        readonly static FieldInfo _autoRotate = AccessTools.Field(typeof(Navigator), "autoRotate");
        readonly static FieldInfo _preciseTarget = AccessTools.Field(typeof(Navigator), "preciseTarget");
        readonly static FieldInfo _decelerate = AccessTools.Field(typeof(Navigator), "decelerate");


        string objectName = "Unnamed Character";
        Character characterEnum = Character.Null;
        string characterEnumName = "";
        PosterObject poster;
        Texture2D posterTexture = MTM101BaldiDevAPI.AssetMan.Get<PosterObject>("CharacterPosterTemplate").baseTexture;
        string[] posterData = new string[]
        {
            "Unnamed",
            "Unnamed and unloved, the developer forgot to give this character a poster."
        };
        bool hasLooker = false;
        bool hasTrigger = false;
        bool useHeatmap = false;
        bool ignorePlayerOnSpawn = false;
        bool ignoreBelts = false;
        bool grounded = true;
        float minAudioDistance = 10f;
        float maxAudioDistance = 250f;
        NPCFlags flags = NPCFlags.HasPhysicalAppearence | NPCFlags.CanMove;
        string[] tags = new string[0];
        List<RoomCategory> spawnableRooms = new List<RoomCategory>();
        List<WeightedRoomAsset> potentialRoomAssets = new List<WeightedRoomAsset>();
        float fieldOfView = -1f;
        float maxSightDistance = 10000f;
        bool autoRotate = true;
        bool preciseTarget = true;
        bool decelerate = false;

        /// <summary>
        /// Builds the NPC.
        /// </summary>
        /// <returns>The created NPC prefab.</returns>
        public T Build()
        {
            T newNpc = GameObject.Instantiate(MTM101BaldiDevAPI.AssetMan.Get<GameObject>("TemplateNPC")).AddComponent<T>();
            Entity npcEntity = newNpc.GetComponent<Entity>();
            newNpc.name = objectName;
            Navigator nav = newNpc.GetComponent<Navigator>();
            // initialize private fields for the npc
            Character character = characterEnum;
            if (characterEnumName != "")
            {
                character = EnumExtensions.ExtendEnum<Character>(characterEnumName);
            }
            _character.SetValue(newNpc, character);
            _navigator.SetValue(newNpc, nav);
            if (!flags.HasFlag(NPCFlags.CanMove))
            {
                nav.enabled = false;
            }
            if (poster == null)
            {
                poster = ObjectCreators.CreateCharacterPoster(posterTexture, posterData[0], posterData[1]);
            }
            _poster.SetValue(newNpc, poster);
            // set up proper sprite data
            if (flags.HasFlag(NPCFlags.HasPhysicalAppearence))
            {
                newNpc.spriteBase = newNpc.transform.Find("SpriteBase").gameObject; //technically, yes, we could just use .GetChild(0), but I refer to it by name incase something changes to avoid grabbing the wrong thing
                newNpc.spriteRenderer = new SpriteRenderer[] { newNpc.spriteBase.transform.GetChild(0).GetComponent<SpriteRenderer>() };
            }
            else
            {
                newNpc.spriteBase = null;
                newNpc.spriteRenderer = new SpriteRenderer[] { };
            }
            newNpc.baseTrigger = newNpc.GetComponents<CapsuleCollider>().Where(x => x.isTrigger).ToArray();
            newNpc.looker = newNpc.GetComponent<Looker>();
            newNpc.looker.enabled = hasLooker;
            if (hasLooker)
            {
                flags |= NPCFlags.CanSee;
            }
            _useHeatMap.SetValue(nav, useHeatmap);
            _npc.SetValue(newNpc.looker, newNpc);
            if (spawnableRooms.Count == 0)
            {
                newNpc.spawnableRooms = new List<RoomCategory>() { RoomCategory.Hall };
            }
            else
            {
                newNpc.spawnableRooms = spawnableRooms.ToList();
            }
            newNpc.potentialRoomAssets = potentialRoomAssets.ToArray();
            if (hasTrigger)
            {
                _iEntityTrigger.SetValue(npcEntity, new IEntityTrigger[] { newNpc });
                flags |= NPCFlags.HasTrigger;
            }
            PropagatedAudioManager audMan = newNpc.GetComponent<PropagatedAudioManager>();
            _minDistance.SetValue(audMan, minAudioDistance);
            _maxDistance.SetValue(audMan, maxAudioDistance);
            nav.npc = newNpc;
            _entity.SetValue(nav, npcEntity);
            _collider.SetValue(nav, newNpc.baseTrigger[0]);
            _ignoreBelts.SetValue(newNpc, ignoreBelts);
            _ignorePlayerOnSpawn.SetValue(newNpc, ignorePlayerOnSpawn);
            npcEntity.SetGrounded(grounded);

            if (fieldOfView >= 0f)
            {
                _hasFov.SetValue(newNpc.looker, true);
                _fieldOfView.SetValue(newNpc.looker, fieldOfView);
            }
            _autoRotate.SetValue(newNpc.Navigator, autoRotate);
            _preciseTarget.SetValue(newNpc.Navigator, preciseTarget);
            _decelerate.SetValue(newNpc.Navigator, decelerate);
            newNpc.looker.distance = maxSightDistance;

            newNpc.gameObject.ConvertToPrefab(true);
            NPCMetadata meta = newNpc.AddMeta(info.Instance, flags);
            meta.tags.AddRange(tags);

            return newNpc;
        }

        /// <summary>
        /// Makes the NPC target the tile instead of the exact cordinate.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> DisableNavigationPrecision()
        {
            preciseTarget = false;
            return this;
        }

        /// <summary>
        /// Disables the NPC from automatically rotating in the direction it is moving.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> DisableAutoRotation()
        {
            autoRotate = false;
            return this;
        }

        /// <summary>
        /// Sets the max distance the NPC can see, one tile is 10 units.
        /// </summary>
        /// <param name="maxDistance"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetMaxSightDistance(float maxDistance)
        {
            maxSightDistance = maxDistance;
            return this;
        }

        /// <summary>
        /// Assigns a field of view to the NPC, as default NPCs can see all around them.
        /// </summary>
        /// <param name="fov">The FOV to give the NPC</param>
        /// <returns></returns>
        public NPCBuilder<T> SetFOV(float fov)
        {
            fieldOfView = fov;
            return this;
        }

        /// <summary>
        /// Sets the name of the NPC Object
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetName(string name)
        {
            objectName = name;
            return this;
        }

        /// <summary>
        /// Sets the enum of the NPC to an already existing enum.
        /// </summary>
        /// <param name="character"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetEnum(Character character)
        {
            characterEnum = character;
            return this;
        }

        /// <summary>
        /// Sets the enum of the NPC to an automatically created one with the same name.
        /// </summary>
        /// <param name="enumName"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetEnum(string enumName)
        {
            characterEnum = Character.Null;
            characterEnumName = enumName;
            return this;
        }

        /// <summary>
        /// Sets the NPC's poster to the specified poster object.
        /// </summary>
        /// <param name="poster"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetPoster(PosterObject poster)
        {
            this.poster = poster;
            return this;
        }

        /// <summary>
        /// Adds a flag to the metadata
        /// </summary>
        /// <param name="flag"></param>
        /// <returns></returns>
        public NPCBuilder<T> AddMetaFlag(NPCFlags flag)
        {
            flags |= flag;
            return this;
        }

        /// <summary>
        /// Changes the NPC's navigator to use acceleration.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> EnableAcceleration()
        {
            decelerate = true;
            return this;
        }

        /// <summary>
        /// Makes the NPC unable to move.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> SetStationary()
        {
            flags &= NPCFlags.CanMove;
            return this;
        }

        /// <summary>
        /// Sets the tags for the NPC.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public NPCBuilder<T> SetMetaTags(string[] tags)
        {
            this.tags = tags;
            return this;
        }

        /// <summary>
        /// Remove the NPC's visuals completely. Only use this if you plan on having your character be entirely invisible all the time.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> RemoveSprite()
        {
            flags &= NPCFlags.HasPhysicalAppearence;
            return this;
        }

        /// <summary>
        /// Sets the NPC poster to an automatically generated one.
        /// </summary>
        /// <param name="texture">The texture to use for the automatically generated poster.</param>
        /// <param name="posterTitle">The NPC's name to show on the poster.</param>
        /// <param name="posterDescription">The NPC's description to show on the poster.</param>
        /// <returns></returns>
        public NPCBuilder<T> SetPoster(Texture2D texture, string posterTitle, string posterDescription)
        {
            poster = null;
            posterTexture = texture;
            posterData = new string[] { posterTitle, posterDescription };
            return this;
        }

        /// <summary>
        /// Adds a looker, allowing the NPC to see the player.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> AddLooker()
        {
            hasLooker = true;
            return this;
        }

        /// <summary>
        /// Adds a trigger, allowing the NPC to collide with entities and the player.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> AddTrigger()
        {
            hasTrigger = true;
            return this;
        }

        /// <summary>
        /// Makes the NPC spawn instantly, regardless of distance from the player.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> IgnorePlayerOnSpawn()
        {
            ignorePlayerOnSpawn = true;
            return this;
        }

        /// <summary>
        /// Makes the NPC ignore conveyor belts.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> IgnoreBelts()
        {
            ignoreBelts = true;
            return this;
        }

        /// <summary>
        /// Makes the NPC count as airborne.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> SetAirborne()
        {
            grounded = false;
            return this;
        }

        /// <summary>
        /// Makes the NPC have the ability to use the heatmap for navigation.
        /// </summary>
        /// <returns></returns>
        public NPCBuilder<T> AddHeatmap()
        {
            useHeatmap = true;
            return this;
        }

        /// <summary>
        /// Add multiple room categories as potential spawn locations for the NPC.
        /// </summary>
        /// <param name="categories"></param>
        /// <returns></returns>
        public NPCBuilder<T> AddSpawnableRoomCategories(params RoomCategory[] categories)
        {
            spawnableRooms.AddRange(categories);
            return this;
        }

        /// <summary>
        /// Set the Min/Max Audio distance for the NPC's sounds. One tile is 10 units.
        /// </summary>
        /// <param name="min">Minimum distance for the audio manager.</param>
        /// <param name="max">Maximum distance for the audio manager.</param>
        /// <returns></returns>
        public NPCBuilder<T> SetMinMaxAudioDistance(float min, float max)
        {
            minAudioDistance = min;
            maxAudioDistance = max;
            return this;
        }

        /// <summary>
        /// Add a room asset that the NPC will spawn into the level, like Gotta Sweep's closet or Dr. Reflex's office.
        /// </summary>
        /// <param name="asset"></param>
        /// <param name="weight"></param>
        /// <returns></returns>
        public NPCBuilder<T> AddPotentialRoomAsset(RoomAsset asset, int weight)
        {
            potentialRoomAssets.Add(new WeightedRoomAsset()
            {
                selection=asset,
                weight=weight
            });
            return this;
        }

        /// <summary>
        /// Add multiple room assets that the NPC will spawn one of into the level, like Gotta Sweep's closet or Dr. Reflex's office.
        /// </summary>
        /// <param name="assets"></param>
        /// <returns></returns>
        public NPCBuilder<T> AddPotentialRoomAssets(params WeightedRoomAsset[] assets)
        {
            potentialRoomAssets.AddRange(assets);
            return this;
        }
    }
}
