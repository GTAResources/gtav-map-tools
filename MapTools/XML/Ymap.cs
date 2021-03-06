﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Xml.Linq;

namespace MapTools.XML
{
    public class Ymap
    {
        public string filename { get; set; }
        public CMapData CMapData { get; set; }

        public Ymap(string name)
        {
            filename = name;
            CMapData = new CMapData(name);
        }

        public XDocument WriteXML()
        {
            return new XDocument(new XDeclaration("1.0", "UTF-8", "no"), CMapData.WriteXML());
        }

        public Ymap(XDocument document, string name)
        {
            filename = name;
            CMapData = new CMapData(document.Element("CMapData"));
        }

        public void UpdatelodDist(List<CBaseArchetypeDef> archetypes)
        {
            if (archetypes?.Any() ?? false)
            {
                foreach (CEntityDef ent in CMapData.entities)
                {
                    CBaseArchetypeDef arc = archetypes.Where(a => a.name == ent.archetypeName).FirstOrDefault();
                    if (arc != null)
                        ent.lodDist = 100 + (1.5f * arc.bsRadius);
                    else
                        Console.WriteLine("Missing CBaseArchetypeDef: " + ent.archetypeName);
                }
            }
            else
            {
                foreach (CEntityDef ent in CMapData.entities)
                    Console.WriteLine("Missing CBaseArchetypeDef: " + ent.archetypeName);
            }
        }

        public void MoveYmap(Vector3 offset)
        {
            CMapData.streamingExtentsMax += offset;
            CMapData.streamingExtentsMin += offset;
            CMapData.entitiesExtentsMax += offset;
            CMapData.entitiesExtentsMin += offset;

            foreach (CEntityDef entity in CMapData.entities)
                entity.position += offset;

            GrassInstance[] batches = CMapData.instancedData.GrassInstanceList.ToArray();
            for (int i = 0; i < batches.Length; i++)
            {
                BatchAABB bb = batches[i].BatchAABB;
                bb.min += new Vector4(offset,0);
                bb.max += new Vector4(offset, 0);
                batches[i].BatchAABB = bb;
            }
            CMapData.instancedData.GrassInstanceList = batches.ToList();

        }

        public void EditInstancedGrassColor(byte[] min,byte[] max)
        {
            Random rnd = new Random();
            GrassInstance[] batches = CMapData.instancedData.GrassInstanceList.ToArray();
            for (int i = 0; i < batches.Length; i++)
            {
                Instance[] instances = batches[i].InstanceList.ToArray();
                for (int j = 0; j < instances.Length; j++)
                {
                    instances[j].Color[0] = (byte)rnd.Next(min[0],max[0]);
                    instances[j].Color[1] = (byte)rnd.Next(min[1], max[1]);
                    instances[j].Color[2] = (byte)rnd.Next(min[2], max[2]);
                }
                batches[i].InstanceList = instances.ToList();
            }
            CMapData.instancedData.GrassInstanceList = batches.ToList();
        }

        //USES XYZ ROTATION IN DEGREES
        public int MoveAndRotateEntitiesByName(string entityname, Vector3 positionOffset, Vector3 rotationOffset)
        {
            int i = 0;
            Vector3 radians = rotationOffset * (float)Math.PI / 180;
            Quaternion quaternionOffset = Quaternion.CreateFromYawPitchRoll(radians.Y, radians.X, radians.Z);

            foreach (CEntityDef entity in CMapData.entities)
            {
                if (entity.archetypeName == entityname)
                {
                    entity.position += positionOffset;
                    entity.rotation = Quaternion.Multiply(entity.rotation, quaternionOffset);
                    i++;
                }
            }
            return i;
        }

        public int MoveAndRotateEntitiesByName(string entityname, Vector3 positionOffset, Quaternion rotationOffset)
        {
            int i = 0;
            foreach (CEntityDef entity in CMapData.entities)
            {
                if (entity.archetypeName == entityname)
                {
                    entity.position += positionOffset;
                    entity.rotation = Quaternion.Multiply(entity.rotation, rotationOffset);
                    i++;
                }
            }
            return i;
        }

        public List<CEntityDef> RemoveEntitiesByNames(List<string> removelist)
        {
            List<CEntityDef> removed = new List<CEntityDef>();
            if (removelist == null || removelist.Count < 1)
                return removed;
            List<CEntityDef> entities_new = new List<CEntityDef>();

            if (CMapData.entities?.Any() ?? false)
            {
                foreach (CEntityDef entity in CMapData.entities)
                {
                    if (removelist.Contains(entity.archetypeName))
                        removed.Add(entity);
                    else
                        entities_new.Add(entity);
                }
            }
            CMapData.entities = entities_new;
            return removed;
        }

        //UPDATES THE EXTENTS OF A CMAPDATA AND RETURNS NAMES OF THE MISSING ARCHETYPES TO WARN ABOUT INACCURATE CALCULATION
        public HashSet<string> UpdateExtents(List<CBaseArchetypeDef> archetypes)
        {
            HashSet<string> missing = new HashSet<string>();
            Vector3 entities_entMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 entities_entMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 entities_strMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 entities_strMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            if (CMapData.entities?.Any() ?? false)
            {
                foreach (CEntityDef entity in CMapData.entities)
                {
                    CBaseArchetypeDef selected = null;
                    if (archetypes?.Any() ?? false)
                        selected = archetypes.Where(a => a.name == entity.archetypeName).FirstOrDefault();

                    float lodDist = entity.lodDist;

                    if (selected != null)
                    {
                        if (entity.lodDist <= 0.0f)
                            lodDist = selected.lodDist;

                        Vector3 aabbmax = Vector3.Transform(selected.bbMax, entity.rotation);
                        Vector3 aabbmin = Vector3.Transform(selected.bbMin, entity.rotation);
                        Vector3 centroid = Vector3.Transform(selected.bsCentre, entity.rotation);

                        entities_entMax.X = Math.Max(entities_entMax.X, entity.position.X + aabbmax.X + centroid.X);
                        entities_entMax.Y = Math.Max(entities_entMax.Y, entity.position.Y + aabbmax.Y + centroid.Y);
                        entities_entMax.Z = Math.Max(entities_entMax.Z, entity.position.Z + aabbmax.Z + centroid.Z);

                        entities_entMin.X = Math.Min(entities_entMin.X, entity.position.X + aabbmin.X + centroid.X);
                        entities_entMin.Y = Math.Min(entities_entMin.Y, entity.position.Y + aabbmin.Y + centroid.Y);
                        entities_entMin.Z = Math.Min(entities_entMin.Z, entity.position.Z + aabbmin.Z + centroid.Z);

                        entities_strMax.X = Math.Max(entities_strMax.X, entity.position.X + aabbmax.X + centroid.X + lodDist);
                        entities_strMax.Y = Math.Max(entities_strMax.Y, entity.position.Y + aabbmax.Y + centroid.Y + lodDist);
                        entities_strMax.Z = Math.Max(entities_strMax.Z, entity.position.Z + aabbmax.Z + centroid.Z + lodDist);

                        entities_strMin.X = Math.Min(entities_strMin.X, entity.position.X + aabbmin.X + centroid.X - lodDist);
                        entities_strMin.Y = Math.Min(entities_strMin.Y, entity.position.Y + aabbmin.Y + centroid.Y - lodDist);
                        entities_strMin.Z = Math.Min(entities_strMin.Z, entity.position.Z + aabbmin.Z + centroid.Z - lodDist);
                    }
                    else
                    {
                        missing.Add(entity.archetypeName);

                        entities_entMax.X = Math.Max(entities_entMax.X, entity.position.X);
                        entities_entMax.Y = Math.Max(entities_entMax.Y, entity.position.Y);
                        entities_entMax.Z = Math.Max(entities_entMax.Z, entity.position.Z);

                        entities_entMin.X = Math.Min(entities_entMin.X, entity.position.X);
                        entities_entMin.Y = Math.Min(entities_entMin.Y, entity.position.Y);
                        entities_entMin.Z = Math.Min(entities_entMin.Z, entity.position.Z);

                        entities_strMax.X = Math.Max(entities_strMax.X, entity.position.X + lodDist);
                        entities_strMax.Y = Math.Max(entities_strMax.Y, entity.position.Y + lodDist);
                        entities_strMax.Z = Math.Max(entities_strMax.Z, entity.position.Z + lodDist);

                        entities_strMin.X = Math.Min(entities_strMin.X, entity.position.X - lodDist);
                        entities_strMin.Y = Math.Min(entities_strMin.Y, entity.position.Y - lodDist);
                        entities_strMin.Z = Math.Min(entities_strMin.Z, entity.position.Z - lodDist);
                    }
                }
            }

            Vector3 grass_entMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 grass_entMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 grass_strMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 grass_strMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            if (CMapData.instancedData.GrassInstanceList != null)
            {
                foreach (GrassInstance item in CMapData.instancedData.GrassInstanceList)
                {
                    grass_entMax.X = Math.Max(grass_entMax.X, item.BatchAABB.max.X);
                    grass_entMax.Y = Math.Max(grass_entMax.Y, item.BatchAABB.max.Y);
                    grass_entMax.Z = Math.Max(grass_entMax.Z, item.BatchAABB.max.Z);

                    grass_entMin.X = Math.Min(grass_entMin.X, item.BatchAABB.min.X);
                    grass_entMin.Y = Math.Min(grass_entMin.Y, item.BatchAABB.min.Y);
                    grass_entMin.Z = Math.Min(grass_entMin.Z, item.BatchAABB.min.Z);

                    grass_strMax.X = Math.Max(grass_strMax.X, item.BatchAABB.max.X + item.lodDist);
                    grass_strMax.Y = Math.Max(grass_strMax.Y, item.BatchAABB.max.Y + item.lodDist);
                    grass_strMax.Z = Math.Max(grass_strMax.Z, item.BatchAABB.max.Z + item.lodDist); // - 100 Seams a common thing for GrassInstance-only ymaps

                    grass_strMin.X = Math.Min(grass_strMin.X, item.BatchAABB.min.X - item.lodDist);
                    grass_strMin.Y = Math.Min(grass_strMin.Y, item.BatchAABB.min.Y - item.lodDist);
                    grass_strMin.Z = Math.Min(grass_strMin.Z, item.BatchAABB.min.Z - item.lodDist); // + 100 Seams a common thing for GrassInstance-only ymaps
                }
            }

            Vector3 distantlights_entMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 distantlights_entMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            Vector3 distantlights_strMin = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 distantlights_strMax = new Vector3(float.MinValue, float.MinValue, float.MinValue);

            if (CMapData.DistantLODLightsSOA.position != null)
            {
                for (int i = 0; i < CMapData.DistantLODLightsSOA.position.Count; i++)
                {
                    //This probably also requires direction data which is stored in _lodlights.ymap whose extents are the same of their linked _distantlights.ymap

                    byte[] rgbibytes = BitConverter.GetBytes(CMapData.DistantLODLightsSOA.RGBI[i]);
                    float intensity = (rgbibytes[3] * 32.0f / 255); // I am not sure if this is correct

                    distantlights_entMax.X = Math.Max(distantlights_entMax.X, CMapData.DistantLODLightsSOA.position[i].X + intensity);
                    distantlights_entMax.Y = Math.Max(distantlights_entMax.Y, CMapData.DistantLODLightsSOA.position[i].Y + intensity);
                    distantlights_entMax.Z = Math.Max(distantlights_entMax.Z, CMapData.DistantLODLightsSOA.position[i].Z + intensity);

                    distantlights_entMin.X = Math.Min(distantlights_entMin.X, CMapData.DistantLODLightsSOA.position[i].X - intensity);
                    distantlights_entMin.Y = Math.Min(distantlights_entMin.Y, CMapData.DistantLODLightsSOA.position[i].Y - intensity);
                    distantlights_entMin.Z = Math.Min(distantlights_entMin.Z, CMapData.DistantLODLightsSOA.position[i].Z - intensity);

                    distantlights_strMax.X = Math.Max(distantlights_strMax.X, CMapData.DistantLODLightsSOA.position[i].X + 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                    distantlights_strMax.Y = Math.Max(distantlights_strMax.Y, CMapData.DistantLODLightsSOA.position[i].Y + 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                    distantlights_strMax.Z = Math.Max(distantlights_strMax.Z, CMapData.DistantLODLightsSOA.position[i].Z + 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                    distantlights_strMin.X = Math.Min(distantlights_strMin.X, CMapData.DistantLODLightsSOA.position[i].X - 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                    distantlights_strMin.Y = Math.Min(distantlights_strMin.Y, CMapData.DistantLODLightsSOA.position[i].Y - 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                    distantlights_strMin.Z = Math.Min(distantlights_strMin.Z, CMapData.DistantLODLightsSOA.position[i].Z - 3000); // Seams a common thing for DistantLODLightsSOA-only ymaps
                }
            }

            CMapData.streamingExtentsMax = new Vector3(
                Math.Max(Math.Max(entities_strMax.X, grass_strMax.X), distantlights_strMax.X),
                Math.Max(Math.Max(entities_strMax.Y, grass_strMax.Y), distantlights_strMax.Y),
                Math.Max(Math.Max(entities_strMax.Z, grass_strMax.Z), distantlights_strMax.Z));
            CMapData.streamingExtentsMin = new Vector3(
                Math.Min(Math.Min(entities_strMin.X, grass_strMin.X), distantlights_strMin.X),
                Math.Min(Math.Min(entities_strMin.Y, grass_strMin.Y), distantlights_strMin.Y),
                Math.Min(Math.Min(entities_strMin.Z, grass_strMin.Z), distantlights_strMin.Z));
            CMapData.entitiesExtentsMax = new Vector3(
                Math.Max(Math.Max(entities_entMax.X, grass_entMax.X), distantlights_entMax.X),
                Math.Max(Math.Max(entities_entMax.Y, grass_entMax.Y), distantlights_entMax.Y),
                Math.Max(Math.Max(entities_entMax.Z, grass_entMax.Z), distantlights_entMax.Z));
            CMapData.entitiesExtentsMin = new Vector3(
                Math.Min(Math.Min(entities_entMin.X, grass_entMin.X), distantlights_entMin.X),
                Math.Min(Math.Min(entities_entMin.Y, grass_entMin.Y), distantlights_entMin.Y),
                Math.Min(Math.Min(entities_entMin.Z, grass_entMin.Z), distantlights_entMin.Z));
            return missing;
        }

        /*Vector3 aabbmax = selected.bbMax;
          Vector3 aabbmin = selected.bbMin;
          Vector3 centroid = selected.bsCentre;

          Vector3[] entBox = new Vector3[8];
          entBox[0] = aabbmin;
          entBox[1] = new Vector3(aabbmin.X * entity.scaleXY, aabbmin.Y * entity.scaleXY, aabbmax.Z * entity.scaleZ);
          entBox[2] = new Vector3(aabbmin.X * entity.scaleXY, aabbmax.Y * entity.scaleXY, aabbmin.Z * entity.scaleZ);
          entBox[3] = new Vector3(aabbmin.X * entity.scaleXY, aabbmax.Y * entity.scaleXY, aabbmax.Z * entity.scaleZ);
          entBox[4] = new Vector3(aabbmax.X * entity.scaleXY, aabbmin.Y * entity.scaleXY, aabbmin.Z * entity.scaleZ);
          entBox[5] = new Vector3(aabbmax.X * entity.scaleXY, aabbmin.Y * entity.scaleXY, aabbmax.Z * entity.scaleZ);
          entBox[6] = new Vector3(aabbmax.X * entity.scaleXY, aabbmax.Y * entity.scaleXY, aabbmin.Z * entity.scaleZ);
          entBox[7] = aabbmax;

          Vector3 strBoxMax = aabbmax + (new Vector3(lodDist, lodDist, lodDist));
          Vector3 strBoxMin = aabbmin - (new Vector3(lodDist, lodDist, lodDist));

          Vector3[] strBox = new Vector3[8];
          strBox[0] = strBoxMin;
          strBox[1] = new Vector3(strBoxMin.X * entity.scaleXY, strBoxMin.Y * entity.scaleXY, strBoxMax.Z * entity.scaleZ);
          strBox[2] = new Vector3(strBoxMin.X * entity.scaleXY, strBoxMax.Y * entity.scaleXY, strBoxMin.Z * entity.scaleZ);
          strBox[3] = new Vector3(strBoxMin.X * entity.scaleXY, strBoxMax.Y * entity.scaleXY, strBoxMax.Z * entity.scaleZ);
          strBox[4] = new Vector3(strBoxMax.X * entity.scaleXY, strBoxMin.Y * entity.scaleXY, strBoxMin.Z * entity.scaleZ);
          strBox[5] = new Vector3(strBoxMax.X * entity.scaleXY, strBoxMin.Y * entity.scaleXY, strBoxMax.Z * entity.scaleZ);
          strBox[6] = new Vector3(strBoxMax.X * entity.scaleXY, strBoxMax.Y * entity.scaleXY, strBoxMin.Z * entity.scaleZ);
          strBox[7] = strBoxMax;

          for (int i = 0; i < 8; i++)
          {
              entMax.X = Math.Max(entMax.X, Vector3.Transform(entBox[i], entity.rotation).X);
              entMax.Y = Math.Max(entMax.Y, Vector3.Transform(entBox[i], entity.rotation).Y);
              entMax.Z = Math.Max(entMax.Z, Vector3.Transform(entBox[i], entity.rotation).Z);

              entMin.X = Math.Max(entMin.X, Vector3.Transform(entBox[i], entity.rotation).X);
              entMin.Y = Math.Max(entMin.Y, Vector3.Transform(entBox[i], entity.rotation).Y);
              entMin.Z = Math.Max(entMin.Z, Vector3.Transform(entBox[i], entity.rotation).Z);

              strMax.X = Math.Max(strMax.X, Vector3.Transform(strBox[i], entity.rotation).X);
              strMax.Y = Math.Max(strMax.Y, Vector3.Transform(strBox[i], entity.rotation).Y);
              strMax.Z = Math.Max(strMax.Z, Vector3.Transform(strBox[i], entity.rotation).Z);

              strMin.X = Math.Max(strMin.X, Vector3.Transform(strBox[i], entity.rotation).X);
              strMin.Y = Math.Max(strMin.Y, Vector3.Transform(strBox[i], entity.rotation).Y);
              strMin.Z = Math.Max(strMin.Z, Vector3.Transform(strBox[i], entity.rotation).Z);
           }*/

        public static Ymap Merge(Ymap[] list)
        {
            if (list == null || list.Length < 1)
                return null;
            Ymap merged = new Ymap("merged");
            foreach (Ymap current in list)
            {
                if (current.CMapData.entities?.Any() ?? false)
                {
                    foreach (CEntityDef entity in current.CMapData.entities)
                    {
                        if (!merged.CMapData.entities.Contains(entity))
                            merged.CMapData.entities.Add(entity);
                        else
                            Console.WriteLine("Skipped duplicated CEntityDef " + entity.guid);
                    }
                }

                if (current.CMapData.physicsDictionaries?.Any() ?? false)
                {
                    foreach (string physicsDictionary in current.CMapData.physicsDictionaries)
                    {
                        if (!merged.CMapData.physicsDictionaries.Contains(physicsDictionary))
                            merged.CMapData.physicsDictionaries.Add(physicsDictionary);
                        else
                            Console.WriteLine("Skipped duplicated physicsDictionary " + physicsDictionary);
                    }
                }

                if (current.CMapData.instancedData.GrassInstanceList?.Any() ?? false)
                {
                    foreach (GrassInstance instance in current.CMapData.instancedData.GrassInstanceList)
                    {
                        if (!merged.CMapData.instancedData.GrassInstanceList.Contains(instance))
                            merged.CMapData.instancedData.GrassInstanceList.Add(instance);
                        else
                            Console.WriteLine("Skipped duplicated GrassInstance Item " + instance.archetypeName);
                    }
                }

                if ((current.CMapData.DistantLODLightsSOA.position?.Any() ?? false) && (current.CMapData.DistantLODLightsSOA.RGBI?.Any() ?? false))
                {
                    if (current.CMapData.DistantLODLightsSOA.position.Count == current.CMapData.DistantLODLightsSOA.RGBI.Count)
                    {
                        for (int i = 0; i < current.CMapData.DistantLODLightsSOA.position.Count ; i++)
                        {
                            if (!merged.CMapData.DistantLODLightsSOA.position.Contains(current.CMapData.DistantLODLightsSOA.position[i]))
                            {
                                merged.CMapData.DistantLODLightsSOA.position.Add(current.CMapData.DistantLODLightsSOA.position[i]);
                                merged.CMapData.DistantLODLightsSOA.RGBI.Add(current.CMapData.DistantLODLightsSOA.RGBI[i]);
                            }
                            else
                                Console.WriteLine("Skipped duplicated DistantLODLightsSOA Item " + current.CMapData.DistantLODLightsSOA.position[i].ToString());
                        }
                    }
                    else Console.WriteLine("Skipped DistantLODLightsSOA in {0} (position and RGBI count aren't matching)", current.filename);
                }
            }
            return merged;
        }

        public List<Ymap> GridSplitAll(int block_size)
        {
            List<Ymap> grid = new List<Ymap>();
            int size = 16384;
            int numblocks = (size / block_size) + 1;

            for (int x = -numblocks; x <= numblocks; x++)
            {
                for (int y = -numblocks; y <= numblocks; y++)
                {
                    CMapData current = new CMapData(this.CMapData);

                    float maxX = (x + 1) * block_size;
                    float minX = x * block_size;
                    float maxY = (y + 1) * block_size;
                    float minY = y * block_size;

                    if (CMapData.entities?.Any() ?? false)
                        current.entities = CMapData.entities.Where(entity => entity.position.X < maxX && entity.position.X >= minX && entity.position.Y < maxY && entity.position.Y >= minY).ToList();

                    if (CMapData.instancedData.GrassInstanceList?.Any() ?? false)
                        current.instancedData.GrassInstanceList = CMapData.instancedData.GrassInstanceList.Where(batch =>
                        ((batch.BatchAABB.max + batch.BatchAABB.min) / 2).X < maxX &&
                        ((batch.BatchAABB.max + batch.BatchAABB.min) / 2).X >= minX &&
                        ((batch.BatchAABB.max + batch.BatchAABB.min) / 2).Y < maxY &&
                        ((batch.BatchAABB.max + batch.BatchAABB.min) / 2).Y >= minY).ToList();

                    if (CMapData.DistantLODLightsSOA.position?.Any() ?? false)
                    {
                        current.DistantLODLightsSOA.position = CMapData.DistantLODLightsSOA.position.Where(item => item.X < maxX && item.X >= minX && item.Y < maxY && item.Y >= minY).ToList();
                        current.DistantLODLightsSOA.RGBI = CMapData.DistantLODLightsSOA.RGBI.Where((color, index) => current.DistantLODLightsSOA.position.Contains(CMapData.DistantLODLightsSOA.position[index])).ToList();
                        /*
                        current.DistantLODLightsSOA.position = new List<Vector3>();
                        current.DistantLODLightsSOA.RGBI = new List<uint>();
                        for (int i = 0; i< DistantLODLightsSOA.position.Count; i++)
                        {
                            if (DistantLODLightsSOA.position[i].X < maxX && DistantLODLightsSOA.position[i].X >= minX && DistantLODLightsSOA.position[i].Y < maxY && DistantLODLightsSOA.position[i].Y >= minY)
                            {
                                current.DistantLODLightsSOA.position.Add(DistantLODLightsSOA.position[i]);
                                current.DistantLODLightsSOA.RGBI.Add(DistantLODLightsSOA.RGBI[i]);
                            }
                        }
                        */
                    }

                    if (current.entities.Any() || current.instancedData.GrassInstanceList.Any() || current.DistantLODLightsSOA.position.Any())
                    {
                        string tmpname = filename.Split('.')[0] + "_" + (grid.Count+1).ToString("00") + ".ymap.xml";
                        Ymap tmp = new Ymap(tmpname);
                        tmp.CMapData = current;
                        grid.Add(tmp);
                    }
                }
            }
            return grid;
        }
    }

    public class CMapData
    {
        public string name { get; set; }
        public string parent { get; set; }
        public uint flags { get; set; }
        public uint contentFlags { get; set; }
        public Vector3 streamingExtentsMin { get; set; }
        public Vector3 streamingExtentsMax { get; set; }
        public Vector3 entitiesExtentsMin { get; set; }
        public Vector3 entitiesExtentsMax { get; set; }
        public List<CEntityDef> entities { get; set; }
        public object containerLods { get; set; }
        public object boxOccluders { get; set; }
        public object occludeModels { get; set; }
        public HashSet<string> physicsDictionaries { get; set; }
        public InstancedMapData instancedData;
        public List<CTimeCycleModifier> timeCycleModifiers;
        public List<CCarGen> carGenerators { get; set; }
        public CLODLight LODLightsSOA;
        public CDistantLODLight DistantLODLightsSOA;
        public CBlockDesc block { get; set; }

        public CMapData(CMapData map)
        {
            this.name = map.name;
            this.parent = map.parent;
            this.flags = map.flags;
            this.contentFlags = map.contentFlags;
            this.streamingExtentsMin = map.streamingExtentsMin;
            this.streamingExtentsMax = map.streamingExtentsMax;
            this.entitiesExtentsMin = map.entitiesExtentsMin;
            this.entitiesExtentsMax = map.entitiesExtentsMax;
            this.entities = map.entities;
            this.containerLods = map.containerLods;
            this.boxOccluders = map.boxOccluders;
            this.occludeModels = map.occludeModels;
            this.physicsDictionaries = map.physicsDictionaries;
            this.instancedData = map.instancedData;
            this.timeCycleModifiers = map.timeCycleModifiers;
            this.carGenerators = map.carGenerators;
            this.LODLightsSOA = map.LODLightsSOA;
            this.DistantLODLightsSOA = map.DistantLODLightsSOA;
            this.block = map.block;
        }

        public CMapData(string filename)
        {
            name = filename;
            entities = new List<CEntityDef>();
            physicsDictionaries = new HashSet<string>();
            instancedData = new InstancedMapData();
            instancedData.GrassInstanceList = new List<GrassInstance>();
            timeCycleModifiers = new List<CTimeCycleModifier>();
            carGenerators = new List<CCarGen>();
            DistantLODLightsSOA = new CDistantLODLight();
            DistantLODLightsSOA.position = new List<Vector3>();
            DistantLODLightsSOA.RGBI = new List<uint>();
            block = new CBlockDesc(0, 0, "GTADrifting", "Neos7's MapTools" , Environment.UserName);
        }

        public CMapData(XElement node)
        {
            name = node.Element("name").Value;
            parent = node.Element("parent").Value;
            flags = uint.Parse(node.Element("flags").Attribute("value").Value);
            contentFlags = uint.Parse(node.Element("contentFlags").Attribute("value").Value);
            streamingExtentsMin = new Vector3(
                float.Parse(node.Element("streamingExtentsMin").Attribute("x").Value),
                float.Parse(node.Element("streamingExtentsMin").Attribute("y").Value),
                float.Parse(node.Element("streamingExtentsMin").Attribute("z").Value)
                );
            streamingExtentsMax = new Vector3(
                float.Parse(node.Element("streamingExtentsMax").Attribute("x").Value),
                float.Parse(node.Element("streamingExtentsMax").Attribute("y").Value),
                float.Parse(node.Element("streamingExtentsMax").Attribute("z").Value)
                );
            entitiesExtentsMin = new Vector3(
                float.Parse(node.Element("entitiesExtentsMin").Attribute("x").Value),
                float.Parse(node.Element("entitiesExtentsMin").Attribute("y").Value),
                float.Parse(node.Element("entitiesExtentsMin").Attribute("z").Value)
                );
            entitiesExtentsMax = new Vector3(
                float.Parse(node.Element("entitiesExtentsMax").Attribute("x").Value),
                float.Parse(node.Element("entitiesExtentsMax").Attribute("y").Value),
                float.Parse(node.Element("entitiesExtentsMax").Attribute("z").Value)
                );

            entities = new List<CEntityDef>();
            if (node.Element("entities").Elements()?.Any() ?? false)
            {
                foreach (XElement ent in node.Element("entities").Elements())
                {
                    if (ent.Attribute("type").Value == "CEntityDef")
                        entities.Add(new CEntityDef(ent));
                    else
                        Console.WriteLine("Skipped unsupported entity: " + ent.Attribute("type").Value);
                }
            }

            //containerLods
            //boxOccluders
            //occludeModels

            physicsDictionaries = new HashSet<string>();
            if (node.Element("physicsDictionaries").Elements()?.Any() ?? false)
            {
                foreach (XElement phDict in node.Element("physicsDictionaries").Elements())
                {
                    if (phDict.Name == "Item")
                        physicsDictionaries.Add(phDict.Value);
                }
            }

            instancedData = new InstancedMapData(node.Element("instancedData"));
            //timeCycleModifiers
            //carGenerators

            LODLightsSOA = new CLODLight(node.Element("LODLightsSOA"));
            DistantLODLightsSOA = new CDistantLODLight(node.Element("DistantLODLightsSOA"));
            block = new CBlockDesc(node.Element("block"));
        }

        public XElement WriteXML()
        {
            //CMapData
            XElement CMapDataNode = new XElement("CMapData");

            CMapDataNode.Add(new XElement("name", name));
            CMapDataNode.Add(new XElement("parent", parent ?? string.Empty));
            CMapDataNode.Add(new XElement("flags", new XAttribute("value", flags.ToString())));
            CMapDataNode.Add(new XElement("contentFlags", new XAttribute("value", contentFlags.ToString())));
            CMapDataNode.Add(new XElement("streamingExtentsMin",
                new XAttribute("x", streamingExtentsMin.X.ToString()),
                new XAttribute("y", streamingExtentsMin.Y.ToString()),
                new XAttribute("z", streamingExtentsMin.Z.ToString())
                ));
            CMapDataNode.Add(new XElement("streamingExtentsMax",
                new XAttribute("x", streamingExtentsMax.X.ToString()),
                new XAttribute("y", streamingExtentsMax.Y.ToString()),
                new XAttribute("z", streamingExtentsMax.Z.ToString())
                ));
            CMapDataNode.Add(new XElement("entitiesExtentsMin",
                new XAttribute("x", entitiesExtentsMin.X.ToString()),
                new XAttribute("y", entitiesExtentsMin.Y.ToString()),
                new XAttribute("z", entitiesExtentsMin.Z.ToString())
                ));
            CMapDataNode.Add(new XElement("entitiesExtentsMax",
                new XAttribute("x", entitiesExtentsMax.X.ToString()),
                new XAttribute("y", entitiesExtentsMax.Y.ToString()),
                new XAttribute("z", entitiesExtentsMax.Z.ToString())
                ));

            //entities
            XElement entitiesNode = new XElement("entities");
            CMapDataNode.Add(entitiesNode);

            if (entities?.Any() ?? false)
            {
                foreach (CEntityDef entity in entities)
                    entitiesNode.Add(entity.WriteXML());
            }

            CMapDataNode.Add(new XElement("containerLods"));
            CMapDataNode.Add(new XElement("boxOccluders"));
            CMapDataNode.Add(new XElement("occludeModels"));

            //physicsDictionaries
            XElement physicsDictionariesNode = new XElement("physicsDictionaries");
            CMapDataNode.Add(physicsDictionariesNode);

            if (physicsDictionaries?.Any() ?? false)
            {
                foreach (string phDict in physicsDictionaries)
                    physicsDictionariesNode.Add(new XElement("Item", phDict));
            }

            CMapDataNode.Add(instancedData.WriteXML());
            CMapDataNode.Add(new XElement("timeCycleModifiers"));
            CMapDataNode.Add(new XElement("carGenerators"));

            CMapDataNode.Add(LODLightsSOA.WriteXML());
            CMapDataNode.Add(DistantLODLightsSOA.WriteXML());
            CMapDataNode.Add(block.WriteXML());

            return CMapDataNode;
        }
    }

    public struct CBlockDesc
    {
        public uint version { get; set; }
        public uint flags { get; set; }
        public string name { get; set; }
        public string exportedBy { get; set; }
        public string owner { get; set; }
        public string time { get; set; }

        public CBlockDesc(uint blockversion, uint blockflags, string blockname, string blockexportedby, string blockowner)
        {
            version = blockversion;
            flags = blockflags;
            name = blockname;
            exportedBy = blockexportedby;
            owner = blockowner;
            time = DateTime.UtcNow.ToString();
        }

        public CBlockDesc(XElement node)
        {
            version = uint.Parse(node.Element("version").Attribute("value").Value);
            flags = uint.Parse(node.Element("flags").Attribute("value").Value);
            name = node.Element("name").Value;
            exportedBy = node.Element("exportedBy").Value;
            owner = node.Element("owner").Value;
            time = node.Element("time").Value;
        }

        public XElement WriteXML()
        {
            XElement blockNode = new XElement("block");
            blockNode.Add(new XElement("version", new XAttribute("value", version)));
            blockNode.Add(new XElement("flags", new XAttribute("value", flags)));
            blockNode.Add(new XElement("name", name));
            blockNode.Add(new XElement("exportedBy", exportedBy));
            blockNode.Add(new XElement("owner", owner));
            blockNode.Add(new XElement("time", time));
            return blockNode;
        }
    }

    public struct CDistantLODLight
    {
        public List<Vector3> position { get; set; }
        public List<uint> RGBI { get; set; }
        public ushort numStreetLights { get; set; }
        public ushort category { get; set; }

        public CDistantLODLight(XElement node)
        {
            position = new List<Vector3>();
            foreach (XElement item in node.Element("position").Elements())
                position.Add(new Vector3(float.Parse(item.Element("x").Attribute("value").Value), float.Parse(item.Element("y").Attribute("value").Value), float.Parse(item.Element("z").Attribute("value").Value)));

            RGBI = new List<uint>();
            foreach (string color in node.Element("RGBI").Value.Split('\n'))
            {
                string check = color.Trim();
                if(check != string.Empty)
                    RGBI.Add(uint.Parse(check));
            }

            numStreetLights = ushort.Parse(node.Element("numStreetLights").Attribute("value").Value);
            category = ushort.Parse(node.Element("category").Attribute("value").Value);
        }

        public XElement WriteXML()
        {
            //DistantLODLightsSOA
            XElement DistantLODLightsSOANode = new XElement("DistantLODLightsSOA");
            XElement positionNode = new XElement("position");

            foreach(Vector3 item in position)
                positionNode.Add(new XElement("Item",
                    new XElement("x", new XAttribute("value", item.X.ToString())),
                    new XElement("y", new XAttribute("value", item.Y.ToString())),
                    new XElement("z", new XAttribute("value", item.Z.ToString()))
                    ));
            DistantLODLightsSOANode.Add(positionNode);
            XElement RGBINode = new XElement("RGBI");

            if (RGBI?.Any() ?? false)
            {
                RGBINode.Add(new XAttribute("content", "int_array"));

                string RGBIvalue = string.Empty;
                foreach (uint color in RGBI)
                    RGBIvalue += Environment.NewLine + (new string(' ', 6)) + color.ToString();
                RGBIvalue += Environment.NewLine + (new string(' ', 4));

                RGBINode.Value = RGBIvalue;
            }  
            DistantLODLightsSOANode.Add(RGBINode);
            DistantLODLightsSOANode.Add(new XElement("numStreetLights", new XAttribute("value", numStreetLights.ToString())));
            DistantLODLightsSOANode.Add(new XElement("category", new XAttribute("value", category.ToString())));
            return DistantLODLightsSOANode;
        }
    }

    public struct CLODLight
    {
        public List<Vector3> direction { get; set; }
        public List<float> falloff { get; set; }
        public List<float> falloffExponent { get; set; }
        public List<uint> timeAndStateFlags { get; set; }
        public List<uint> hash { get; set; }
        public List<byte> coneInnerAngle { get; set; }
        public List<byte> coneOuterAngleOrCapExt { get; set; }
        public List<byte> coronaIntensity { get; set; }

        public CLODLight(XElement node)
        {
            direction = new List<Vector3>();
            foreach (XElement item in node.Element("direction").Elements())
                direction.Add(new Vector3(float.Parse(item.Element("x").Attribute("value").Value), float.Parse(item.Element("y").Attribute("value").Value), float.Parse(item.Element("z").Attribute("value").Value)));

            falloff = new List<float>();
            foreach (string item in node.Element("falloff").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    falloff.Add(float.Parse(check));
            }

            falloffExponent = new List<float>();
            foreach (string item in node.Element("falloffExponent").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    falloffExponent.Add(float.Parse(check));
            }

            timeAndStateFlags = new List<uint>();
            foreach (string item in node.Element("timeAndStateFlags").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    timeAndStateFlags.Add(uint.Parse(check));
            }

            hash = new List<uint>();
            foreach (string item in node.Element("hash").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    hash.Add(uint.Parse(check));
            }

            coneInnerAngle = new List<byte>();
            foreach (string item in node.Element("coneInnerAngle").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    coneInnerAngle.Add(byte.Parse(check));
            }

            coneOuterAngleOrCapExt = new List<byte>();
            foreach (string item in node.Element("coneOuterAngleOrCapExt").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    coneOuterAngleOrCapExt.Add(byte.Parse(check));
            }

            coronaIntensity = new List<byte>();
            foreach (string item in node.Element("coronaIntensity").Value.Split('\n'))
            {
                string check = item.Trim();
                if (check != string.Empty)
                    coronaIntensity.Add(byte.Parse(check));
            }
        }

        public XElement WriteXML()
        {
            XElement LODLightsSOANode = new XElement("LODLightsSOA");
            LODLightsSOANode.Add(new XElement("direction"));
            LODLightsSOANode.Add(new XElement("falloff"));
            LODLightsSOANode.Add(new XElement("falloffExponent"));
            LODLightsSOANode.Add(new XElement("timeAndStateFlags"));
            LODLightsSOANode.Add(new XElement("hash"));
            LODLightsSOANode.Add(new XElement("coneInnerAngle"));
            LODLightsSOANode.Add(new XElement("coneOuterAngleOrCapExt"));
            LODLightsSOANode.Add(new XElement("coronaIntensity"));

            if (direction?.Any() ?? false)
            {
                foreach (Vector3 dir in direction)
                    LODLightsSOANode.Element("direction").Add(new XElement("Item",
                        new XElement("x", new XAttribute("value", dir.X)),
                        new XElement("y", new XAttribute("value", dir.Y)),
                        new XElement("z", new XAttribute("value", dir.Z))));
            }
            if (falloff?.Any() ?? false)
            {
                LODLightsSOANode.Element("falloff").Add(new XAttribute("content", "float_array"));
                foreach(float item in falloff)
                    LODLightsSOANode.Element("falloff").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("falloff").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (falloffExponent?.Any() ?? false)
            {
                LODLightsSOANode.Element("falloffExponent").Add(new XAttribute("content", "float_array"));
                foreach (float item in falloffExponent)
                    LODLightsSOANode.Element("falloffExponent").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("falloffExponent").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (timeAndStateFlags?.Any() ?? false)
            {
                LODLightsSOANode.Element("timeAndStateFlags").Add(new XAttribute("content", "int_array"));
                foreach (uint item in timeAndStateFlags)
                    LODLightsSOANode.Element("timeAndStateFlags").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("timeAndStateFlags").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (hash?.Any() ?? false)
            {
                LODLightsSOANode.Element("hash").Add(new XAttribute("content", "int_array"));
                foreach (uint item in hash)
                    LODLightsSOANode.Element("hash").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("hash").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (coneInnerAngle?.Any() ?? false)
            {
                LODLightsSOANode.Element("coneInnerAngle").Add(new XAttribute("content", "char_array"));
                foreach (byte item in coneInnerAngle)
                    LODLightsSOANode.Element("coneInnerAngle").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("coneInnerAngle").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (coneOuterAngleOrCapExt?.Any() ?? false)
            {
                LODLightsSOANode.Element("coneOuterAngleOrCapExt").Add(new XAttribute("content", "char_array"));
                foreach (byte item in coneOuterAngleOrCapExt)
                    LODLightsSOANode.Element("coneOuterAngleOrCapExt").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("coneOuterAngleOrCapExt").Value += Environment.NewLine + (new string(' ', 4));
            }
            if (coronaIntensity?.Any() ?? false)
            {
                LODLightsSOANode.Element("coronaIntensity").Add(new XAttribute("content", "char_array"));
                foreach (byte item in coronaIntensity)
                    LODLightsSOANode.Element("coronaIntensity").Value += Environment.NewLine + (new string(' ', 6) + item.ToString());
                LODLightsSOANode.Element("coronaIntensity").Value += Environment.NewLine + (new string(' ', 4));
            }
            return LODLightsSOANode;
        }
    }

    public struct InstancedMapData
    {
        public object ImapLink { get; set; } //Is this even used by the game?
        public object PropInstanceList { get; set; } //Is this even used by the game?
        public List<GrassInstance> GrassInstanceList { get; set; }

        public InstancedMapData(XElement node)
        {
            ImapLink = null;
            PropInstanceList = null;
            GrassInstanceList = new List<GrassInstance>();
            foreach (XElement item in node.Element("GrassInstanceList").Elements())
                GrassInstanceList.Add(new GrassInstance(item));
        }

        public XElement WriteXML()
        {
            //instancedData
            XElement instancedDataNode = new XElement("instancedData");
            instancedDataNode.Add(new XElement("ImapLink"));
            instancedDataNode.Add(new XElement("PropInstanceList"));

            //GrassInstanceList
            XElement GrassInstanceListNode = new XElement("GrassInstanceList");
            instancedDataNode.Add(GrassInstanceListNode);

            if (GrassInstanceList?.Any() ?? false)
            {
                foreach (GrassInstance GrassInstanceItem in GrassInstanceList)
                    GrassInstanceListNode.Add(GrassInstanceItem.WriteXML());
            }

            return instancedDataNode;
        }
    }

    public struct GrassInstance
    {
        public BatchAABB BatchAABB { get; set; }
        public Vector3 ScaleRange { get; set; }
        public string archetypeName { get; set; }
        public float lodDist { get; set; }
        public float LodFadeStartDist { get; set; }
        public float LodInstFadeRange { get; set; }
        public float OrientToTerrain { get; set; }
        public List<Instance> InstanceList { get; set; }

        public GrassInstance(XElement node)
        {
            BatchAABB = new BatchAABB(
                new Vector4(
                    float.Parse(node.Element("BatchAABB").Element("min").Attribute("x").Value),
                    float.Parse(node.Element("BatchAABB").Element("min").Attribute("y").Value),
                    float.Parse(node.Element("BatchAABB").Element("min").Attribute("z").Value),
                    float.Parse(node.Element("BatchAABB").Element("min").Attribute("w").Value)),
                new Vector4(
                    float.Parse(node.Element("BatchAABB").Element("max").Attribute("x").Value),
                    float.Parse(node.Element("BatchAABB").Element("max").Attribute("y").Value),
                    float.Parse(node.Element("BatchAABB").Element("max").Attribute("z").Value),
                    float.Parse(node.Element("BatchAABB").Element("max").Attribute("w").Value)));
            ScaleRange = new Vector3(
                float.Parse(node.Element("ScaleRange").Attribute("x").Value),
                float.Parse(node.Element("ScaleRange").Attribute("y").Value),
                float.Parse(node.Element("ScaleRange").Attribute("z").Value));
            archetypeName = node.Element("archetypeName").Value.ToLower();
            lodDist = float.Parse(node.Element("lodDist").Attribute("value").Value);
            LodFadeStartDist = float.Parse(node.Element("LodFadeStartDist").Attribute("value").Value);
            LodInstFadeRange = float.Parse(node.Element("LodInstFadeRange").Attribute("value").Value);
            OrientToTerrain = float.Parse(node.Element("OrientToTerrain").Attribute("value").Value);
            InstanceList = new List<Instance>();
            foreach (XElement item in node.Element("InstanceList").Elements())
                InstanceList.Add(new Instance(item));
        }

        public GrassInstance(string name)
        {
            BatchAABB = new BatchAABB(Vector4.Zero, Vector4.Zero);
            ScaleRange = new Vector3 ( 0.6f, 2.3f, 0.2f );
            archetypeName = name;
            lodDist = 50.0f;
            LodFadeStartDist = 20.0f;
            LodInstFadeRange = 0.75f;
            OrientToTerrain = 1.0f;
            InstanceList = new List<Instance>();
        }

        public XElement WriteXML()
        {
            //Item
            XElement ItemNode = new XElement("Item");

            ItemNode.Add(new XElement("BatchAABB",
                new XElement("min",
                    new XAttribute("x", BatchAABB.min.X.ToString()),
                    new XAttribute("y", BatchAABB.min.Y.ToString()),
                    new XAttribute("z", BatchAABB.min.Z.ToString()),
                    new XAttribute("w", BatchAABB.min.W.ToString())
                    ),
                new XElement("max",
                    new XAttribute("x", BatchAABB.max.X.ToString()),
                    new XAttribute("y", BatchAABB.max.Y.ToString()),
                    new XAttribute("z", BatchAABB.max.Z.ToString()),
                    new XAttribute("w", BatchAABB.max.W.ToString())
                    )
                 ));
            ItemNode.Add(new XElement("ScaleRange",
                new XAttribute("x", ScaleRange.X.ToString()),
                new XAttribute("y", ScaleRange.Y.ToString()),
                new XAttribute("z", ScaleRange.Z.ToString())
                ));
            ItemNode.Add(new XElement("archetypeName", archetypeName));
            ItemNode.Add(new XElement("lodDist", new XAttribute("value", lodDist)));
            ItemNode.Add(new XElement("LodFadeStartDist", new XAttribute("value", LodFadeStartDist)));
            ItemNode.Add(new XElement("LodInstFadeRange", new XAttribute("value", LodInstFadeRange)));
            ItemNode.Add(new XElement("OrientToTerrain", new XAttribute("value", OrientToTerrain)));

            //InstanceList
            XElement InstanceListNode = new XElement("InstanceList");
            ItemNode.Add(InstanceListNode);

            if (InstanceList?.Any() ?? false)
            {
                foreach (Instance item in InstanceList)
                    InstanceListNode.Add(item.WriteXML());
            }

            return ItemNode;
        }
    }

    public struct Instance
    {
        public ushort[] Position { get; set; }
        public byte NormalX { get; set; }
        public byte NormalY { get; set; }
        public byte[] Color { get; set; }
        public byte Scale { get; set; }
        public byte Ao { get; set; }
        public byte[] Pad { get; set; }

        public Instance(XElement node)
        {
            Position = new ushort[3] {
                ushort.Parse(node.Element("Position").Value.Split('\n')[1]),
                ushort.Parse(node.Element("Position").Value.Split('\n')[2]),
                ushort.Parse(node.Element("Position").Value.Split('\n')[3])};
            NormalX = byte.Parse(node.Element("NormalX").Attribute("value").Value);
            NormalY = byte.Parse(node.Element("NormalY").Attribute("value").Value);
            Color = new byte[3] {
                byte.Parse(node.Element("Color").Value.Split('\n')[1]),
                byte.Parse(node.Element("Color").Value.Split('\n')[2]),
                byte.Parse(node.Element("Color").Value.Split('\n')[3])};
            Scale = byte.Parse(node.Element("Scale").Attribute("value").Value); ;
            Ao = byte.Parse(node.Element("Ao").Attribute("value").Value); ;
            Pad = new byte[3] {
                byte.Parse(node.Element("Pad").Value.Split('\n')[1]),
                byte.Parse(node.Element("Pad").Value.Split('\n')[2]),
                byte.Parse(node.Element("Pad").Value.Split('\n')[3])};
        }

        public Instance(ushort[] pos, byte normX,byte normY, byte[] col, byte size)
        {
            Position = pos;
            NormalX = normX;
            NormalY = normY;
            Color = col;
            Scale = size;
            Ao = 255;
            Pad = new byte[3] { 0, 0, 0 };
        }

        public XElement WriteXML()
        {
            string PositionNode = (Environment.NewLine + (new string(' ', 14)) + Position[0] + Environment.NewLine + (new string(' ', 14)) + Position[1] + Environment.NewLine + (new string(' ', 14)) + Position[2] + Environment.NewLine + (new string(' ', 12)));
            string ColorNode = (Environment.NewLine + (new string(' ', 14)) + Color[0] + Environment.NewLine + (new string(' ', 14)) + Color[1] + Environment.NewLine + (new string(' ', 14)) + Color[2] + Environment.NewLine + (new string(' ', 12)));
            string PadNode = (Environment.NewLine + (new string(' ', 14)) + Pad[0] + Environment.NewLine + (new string(' ', 14)) + Pad[1] + Environment.NewLine + (new string(' ', 14)) + Pad[2] + Environment.NewLine + (new string(' ', 12)));
            //Item
            XElement ItemNode = new XElement("Item");
            ItemNode.Add(new XElement("Position", new XAttribute("content", "short_array"), PositionNode));
            ItemNode.Add(new XElement("NormalX", new XAttribute("value", NormalX)));
            ItemNode.Add(new XElement("NormalY", new XAttribute("value", NormalY)));
            ItemNode.Add(new XElement("Color", new XAttribute("content", "char_array"), ColorNode));
            ItemNode.Add(new XElement("Scale", new XAttribute("value", Scale)));
            ItemNode.Add(new XElement("Ao", new XAttribute("value", Ao)));
            ItemNode.Add(new XElement("Pad", new XAttribute("content", "char_array"), PadNode));
            return ItemNode;
        }
    }

    public struct BatchAABB
    {
        public Vector4 min { get; set; }
        public Vector4 max { get; set; }

        public BatchAABB(Vector4 batchmin, Vector4 batchmax)
        {
            min = batchmin;
            max = batchmax;
        }
    }

    public struct CTimeCycleModifier
    {
        public string name { get; set; }
        public Vector3 minExtents { get; set; }
        public Vector3 maxExtents { get; set; }
        public float percentage { get; set; }
        public float range { get; set; }
        public uint startHour { get; set; }
        public uint endHour { get; set; }

        /*
        public CTimeCycleModifier(XElement node)
        {
        }
        
         public XElement WriteXML()
         {
         }
         */
    }

    public struct CCarGen
    {
        public Vector3 position { get; set; }
        public float orientX { get; set; }
        public float orientY { get; set; }
        public float perpendicularLength { get; set; }
        public string carModel { get; set; }
        public uint flags { get; set; }
        public int bodyColorRemap1 { get; set; }
        public int bodyColorRemap2 { get; set; }
        public int bodyColorRemap3 { get; set; }
        public int bodyColorRemap4 { get; set; }
        public object popGroup { get; set; }
        public sbyte livery { get; set; }
    }
}
