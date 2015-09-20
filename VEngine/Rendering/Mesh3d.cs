﻿using System;
using System.Collections.Generic;
using System.Linq;
using BulletSharp;
using System.IO;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using System.Text.RegularExpressions;

namespace VEngine
{
    public class Mesh3d : IRenderable, ITransformable
    {
        public Mesh3d(Object3dInfo objectInfo, GenericMaterial material)
        {
            // ModelMatricesBuffer = new ShaderStorageBuffer();
            // RotationMatricesBuffer = new ShaderStorageBuffer();
            DisableDepthWrite = false;
            Instances = 1;
            MainObjectInfo = objectInfo;
            MainMaterial = material;
            Transformation = new TransformationManager(Vector3.Zero, Quaternion.Identity, 1.0f);
            UpdateMatrix();
            MeshColoredID = new Vector3((float)Randomizer.NextDouble(), (float)Randomizer.NextDouble(), (float)Randomizer.NextDouble());
        }

        public Mesh3d()
        {
            // ModelMatricesBuffer = new ShaderStorageBuffer();
            // RotationMatricesBuffer = new ShaderStorageBuffer();
            DisableDepthWrite = false;
            Instances = 1;
            Transformation = new TransformationManager(Vector3.Zero, Quaternion.Identity, 1.0f);
            UpdateMatrix();
            MeshColoredID = new Vector3((float)Randomizer.NextDouble(), (float)Randomizer.NextDouble(), (float)Randomizer.NextDouble());
        }

        public string Name;
        public int Instances;
        public GenericMaterial MainMaterial;
        public Matrix4 Matrix, RotationMatrix;
        public RigidBody PhysicalBody;
        public TransformationManager Transformation;
        public bool DisableDepthWrite;
        public bool Selected = false;
        //public ShaderStorageBuffer ModelMatricesBuffer, RotationMatricesBuffer;

        private float Mass = 1.0f;
        public Object3dInfo MainObjectInfo;
        private CollisionShape PhysicalShape;
        private static Random Randomizer = new Random();
        public Vector3 MeshColoredID;

        public static bool PostProcessingUniformsOnly = false;

        class LodLevelData{
            public Object3dInfo Info3d;
            public GenericMaterial Material;
            public float Distance;
        }

        private List<LodLevelData> LodLevels;
        public static Mesh3d Merge(List<Mesh3d> meshes)
        {
            Object3dInfo main = meshes[0].MainObjectInfo.CopyDeep();
            meshes[0].UpdateMatrix();
            main.Transform(meshes[0].Matrix, meshes[0].RotationMatrix);
            for(int i = 1; i < meshes.Count; i++)
            {
                Object3dInfo obj = meshes[i].MainObjectInfo.CopyDeep();
                meshes[i].UpdateMatrix();
                obj.Transform(meshes[i].Matrix, meshes[i].RotationMatrix);
                main.Append(obj);
            }
            return new Mesh3d(main, meshes[0].MainMaterial);
        }

        public void AddLodLevel(float distance, Object3dInfo info, GenericMaterial material)
        {
            if(LodLevels == null)
                LodLevels = new List<LodLevelData>();
            LodLevels.Add(new LodLevelData()
            {
                Info3d = info,
                Material = material,
                Distance = distance
            });
            LodLevels.Sort((a, b) => (int)((b.Distance - a.Distance)*100.0)); // *100 to preserve precision
        }

        private GenericMaterial GetCurrentMaterial()
        {
            if(LodLevels == null)
                return MainMaterial;
            float distance = (Camera.Current.GetPosition() - Transformation.GetPosition()).Length;
            if(distance < LodLevels.Last().Distance)
                return MainMaterial;
            float d1 = float.MaxValue;
            foreach(var l in LodLevels)
            {
                if(l.Distance < distance)
                    return l.Material;
            }
            return LodLevels.Last().Material;
        }
        private Object3dInfo GetCurrent3dInfo()
        {
            if(LodLevels == null)
                return MainObjectInfo;
            float distance = (Camera.Current.GetPosition() - Transformation.GetPosition()).Length;
            if(distance < LodLevels.Last().Distance)
                return MainObjectInfo;
            float d1 = float.MaxValue;
            foreach(var l in LodLevels)
            {
                if(l.Distance < distance)
                    return l.Info3d;
            }
            return LodLevels.Last().Info3d;
        }



        public RigidBody CreateRigidBody(bool forceRecreate = false)
        {
            if(PhysicalBody != null && !forceRecreate)
                return PhysicalBody;
            bool isDynamic = (Mass != 0.0f);
            var shape = GetCollisionShape();

            Vector3 localInertia = Vector3.Zero;
            if(isDynamic)
                shape.CalculateLocalInertia(Mass, out localInertia);

            DefaultMotionState myMotionState = new DefaultMotionState(Matrix4.CreateFromQuaternion(Transformation.GetOrientation()) *  Matrix4.CreateTranslation(Transformation.GetPosition()));

            RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo(Mass, myMotionState, shape, localInertia);
            RigidBody body = new RigidBody(rbInfo);
            body.UserObject = this;

            PhysicalBody = body;

            return body;
        }
        public RigidBody CreateRigidBody(Vector3 massCenter, bool forceRecreate = false)
        {
            if(PhysicalBody != null && !forceRecreate)
                return PhysicalBody;
            bool isDynamic = (Mass != 0.0f);
            var shape = GetCollisionShape();

            Vector3 localInertia = massCenter;
            if(isDynamic)
                shape.CalculateLocalInertia(Mass, out localInertia);

            DefaultMotionState myMotionState = new DefaultMotionState(Matrix4.CreateFromQuaternion(Transformation.GetOrientation()) *  Matrix4.CreateTranslation(Transformation.GetPosition()));

            RigidBodyConstructionInfo rbInfo = new RigidBodyConstructionInfo(Mass, myMotionState, shape, localInertia);
            RigidBody body = new RigidBody(rbInfo);
            body.UserObject = this;

            PhysicalBody = body;

            return body;
        }

        public void Draw()
        {
            Draw(false);
        }

        public void Draw(bool ignoreDisableDepthWriteFlag = false)
        {
            //if(IsOddframe && DrawOddOnly)
            //    return;
            if(Transformation.HasBeenModified())
            {
                UpdateMatrix();

                //ModelMatricesBuffer.MapData(Matrix);
                //RotationMatricesBuffer.MapData(RotationMatrix);
                Transformation.ClearModifiedFlag();
            }
            if(Camera.Current == null)
                return;

            SetUniforms();
            GetCurrentMaterial().GetShaderProgram().SetUniform("ModelMatrix",  Matrix);
            GetCurrentMaterial().GetShaderProgram().SetUniform("RotationMatrix",  RotationMatrix);

            if(!ignoreDisableDepthWriteFlag)
            {
                if(DisableDepthWrite)
                    OpenTK.Graphics.OpenGL4.GL.DepthMask(false);
                GetCurrent3dInfo().Draw();
                if(DisableDepthWrite)
                    OpenTK.Graphics.OpenGL4.GL.DepthMask(true);
            }
            else
            {
                GetCurrent3dInfo().Draw();
            }

            GLThread.CheckErrors();
        }

        public void SetUniforms()
        {
            bool shaderSwitchResult = GetCurrentMaterial().Use();
            ShaderProgram shader = ShaderProgram.Current;

            //ModelMatricesBuffer.Use(0);
            //RotationMatricesBuffer.Use(1);

            // if(Sun.Current != null) Sun.Current.BindToShader(shader); per mesh
            GLThread.GraphicsSettings.SetUniforms(shader);
            if(!PostProcessingUniformsOnly)
            {
                shader.SetUniform("ColoredID", MeshColoredID); //magic
                shader.SetUniform("ViewMatrix", Camera.Current.ViewMatrix);
                shader.SetUniform("ProjectionMatrix", Camera.Current.ProjectionMatrix);
                
            }
            else
            {
                shader.SetUniform("ViewMatrix", Camera.MainDisplayCamera.ViewMatrix);
                shader.SetUniform("ProjectionMatrix", Camera.MainDisplayCamera.ProjectionMatrix);
            }

            shader.SetUniform("Selected", Selected ? 1 : 0); //magic
            shader.SetUniform("RandomSeed1", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed2", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed3", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed4", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed5", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed6", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed7", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed8", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed9", (float)Randomizer.NextDouble());
            shader.SetUniform("RandomSeed10", (float)Randomizer.NextDouble());
            shader.SetUniform("Time", (float)(DateTime.Now - GLThread.StartTime).TotalMilliseconds / 1000);

            shader.SetUniform("Instances", 0);
            shader.SetUniform("Instanced", 0);
            shader.SetUniform("LogEnchacer", 0.01f);

            shader.SetUniform("CameraPosition", Camera.Current.Transformation.GetPosition());
            shader.SetUniform("CameraDirection", Camera.Current.Transformation.GetOrientation().ToDirection());
            shader.SetUniform("CameraTangentUp", Camera.Current.Transformation.GetOrientation().GetTangent(MathExtensions.TangentDirection.Up));
            shader.SetUniform("CameraTangentLeft", Camera.Current.Transformation.GetOrientation().GetTangent(MathExtensions.TangentDirection.Left));
            shader.SetUniform("FarPlane", Camera.Current.Far);
            shader.SetUniform("resolution", new Vector2(GLThread.Resolution.Width, GLThread.Resolution.Height));

            if(Bones != null)
            {
                shader.SetUniform("UseBoneSystem", 1);
                shader.SetUniform("BonesCount", Bones.Count);
                shader.SetUniformArray("BonesHeads", Bones.Select<Bone, Vector3>((a) => a.Head).ToArray());
                shader.SetUniformArray("BonesTails", Bones.Select<Bone, Vector3>((a) => a.Tail).ToArray());
                shader.SetUniformArray("BonesRotationMatrices", Bones.Select<Bone, Matrix4>((a) => Matrix4.CreateFromQuaternion(a.Orientation)).ToArray());
                shader.SetUniformArray("BonesParents", Bones.Select<Bone, int>((a) =>
                {
                    if(a.Parent == null)
                        return -1;
                    return Bones.IndexOf(a.Parent);
                }).ToArray());
            }
            else
            {
                shader.SetUniform("UseBoneSystem", 0);
            }
        }

        public class Bone
        {
            public string Name, ParentName;
            public Vector3 Head, Tail;
            public Bone Parent;
            public Quaternion Orientation = Quaternion.Identity;
        }

        public List<Bone> Bones = null;

        public void LoadSkeleton(string file, bool bonerelative = false)
        {
            var lines = File.ReadAllLines(file);
            Match match;
            List<Bone> bones = new List<Bone>();
            Bone current = null;
            Vector3 offset = Vector3.Zero;
            foreach(var l in lines)
            {
                if(l.StartsWith("offset"))
                {
                    match = Regex.Match(l, @"offset ([e0-9.-]+) ([e0-9.-]+) ([e0-9.-]+)");
                    float x = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float z = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    offset = new Vector3(x, z, -y);
                }
                if(l.StartsWith("bone"))
                {
                    if(current != null)
                        bones.Add(current);
                    match = Regex.Match(l, @"bone (.+)");
                    current = new Bone()
                    {
                        Name = match.Groups[1].Value
                    };
                }
                if(l.StartsWith("head"))
                {
                    match = Regex.Match(l, @"head ([e0-9.-]+) ([e0-9.-]+) ([e0-9.-]+)");
                    float x = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float z = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    current.Head = new Vector3(x, y, z) + offset;
                }
                if(l.StartsWith("tail"))
                {
                    match = Regex.Match(l, @"tail ([e0-9.-]+) ([e0-9.-]+) ([e0-9.-]+)");
                    float x = float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
                    float z = float.Parse(match.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture);
                    current.Tail = new Vector3(x, y, z) + offset;
                }
                if(l.StartsWith("parent"))
                {
                    match = Regex.Match(l, @"parent (.+)");
                    current.ParentName = match.Groups[1].Value;
                }
            }
            bones.Add(current);
            foreach(var b in bones)
            {
                b.Parent = b.ParentName == "null" ? null : bones.First((a) => a.Name == b.ParentName);
            }
            if(bonerelative)
            {
                Action<Bone> recursion = null;
                recursion = new Action<Bone>((b) =>
                {
                    foreach(var c in bones)
                    {
                        if(c.Parent == b)
                        {
                           // c.Head += b.Head;
                           // c.Tail += b.Head;
                            recursion(c);
                        }
                    }
                });
                recursion(bones.First((a) => a.Parent == null));
                foreach(var b in bones)
                {
                    b.Tail *= 0.612f;
                    b.Head *= 0.612f;
                }
            }
            Bones = bones;
        }

        public CollisionShape GetCollisionShape()
        {
            return PhysicalShape;
        }

        public float GetMass()
        {
            return Mass;
        }

        public TransformationManager GetTransformationManager()
        {
            return Transformation;
        }

        public Mesh3d SetCollisionShape(CollisionShape shape)
        {
            PhysicalShape = shape;
            PhysicalShape.UserObject = this;
            Transformation.MarkAsModified();
            return this;
        }

        public Mesh3d SetMass(float mass)
        {
            Mass = mass;
            Transformation.MarkAsModified();
            return this;
        }

        public void UpdateMatrix(bool noPhysics = false)
        {
            RotationMatrix = Matrix4.CreateFromQuaternion(Transformation.GetOrientation());
            Matrix =  Matrix4.CreateScale(Transformation.GetScale()) * RotationMatrix * Matrix4.CreateTranslation(Transformation.GetPosition());
            if(!noPhysics && PhysicalBody != null)
            {
                PhysicalBody.WorldTransform = RotationMatrix
                    * Matrix4.CreateTranslation(Transformation.GetPosition());
            }
        }
    }
}