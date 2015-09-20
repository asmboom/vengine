﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OpenTK;
using OpenTK.Graphics.OpenGL4;
using VEngine;

namespace AirplanesGame
{
    class Commons
    {
       // public static FreeCamera FreeCam;
        static ProjectionLight RedLight;
        
        public static ProjectionLight AddControllableLight()
        {

            ProjectionLight redConeLight = new ProjectionLight(new Vector3(65, 0, 65), Quaternion.FromAxisAngle(new Vector3(1, 0, -1), MathHelper.Pi / 2), 2048, 2048, MathHelper.PiOver2+0.3f, 0.1f, 10000.0f);
            RedLight = redConeLight;
            redConeLight.LightColor = new Vector4(1, 1, 1, 95);
            //redConeLight.BuildOrthographicProjection(600, 600, -150, 150);

            LightPool.Add(redConeLight);
            

            GLThread.OnUpdate += (o, e) =>
            {
                var kb = OpenTK.Input.Keyboard.GetState();
                if(kb.IsKeyDown(OpenTK.Input.Key.Left))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos + Vector3.UnitX / 12.0f);
                }
                if(kb.IsKeyDown(OpenTK.Input.Key.Right))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos - Vector3.UnitX / 12.0f);
                }
                if(kb.IsKeyDown(OpenTK.Input.Key.Up))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos + Vector3.UnitZ / 12.0f);
                }
                if(kb.IsKeyDown(OpenTK.Input.Key.Down))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos - Vector3.UnitZ / 12.0f);
                }
                if(kb.IsKeyDown(OpenTK.Input.Key.PageUp))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos + Vector3.UnitY / 12.0f);
                }
                if(kb.IsKeyDown(OpenTK.Input.Key.PageDown))
                {
                    var pos = redConeLight.camera.Transformation.GetPosition();
                    redConeLight.camera.Transformation.SetPosition(pos - Vector3.UnitY / 12.0f);
                }
            };
            return redConeLight;
        }

        public static void SetUpInputBehaviours()
        {

            GLThread.OnMouseUp += (o, e) =>
            {
                if(e.Button == OpenTK.Input.MouseButton.Middle)
                {
                    Mesh3d mesh = Camera.Current.RayCastMesh3d();
                    if(mesh != null && mesh.GetCollisionShape() != null)
                    {
                        Console.WriteLine(mesh.GetCollisionShape().ToString());
                        mesh.PhysicalBody.LinearVelocity += (Camera.Current.GetDirection() * 120.0f);

                    }
                }
            };

            MeshLinker.LinkInfo pickedLink = null;
            bool inPickingMode = false;
            GLThread.OnMouseDown += (o, e) =>
            {
                if(e.Button == OpenTK.Input.MouseButton.Right)
                {
                    Mesh3d mesh = Camera.Current.RayCastMesh3d();
                    if(mesh != null && mesh.GetCollisionShape() != null && !mesh.PhysicalBody.IsStaticObject && !inPickingMode)
                    {
                        Console.WriteLine(mesh.GetCollisionShape().ToString());
                        pickedLink = MeshLinker.Link(Camera.MainDisplayCamera, mesh, new Vector3(0, 0, -(Camera.MainDisplayCamera.Transformation.GetPosition() - mesh.Transformation.GetPosition()).Length),
                            Quaternion.Identity);
                        pickedLink.UpdateRotation = false;
                        inPickingMode = true;
                    }
                }
            };
            GLThread.OnMouseUp += (o, e) =>
            {
                if(e.Button == OpenTK.Input.MouseButton.Right)
                {
                    MeshLinker.Unlink(pickedLink);
                    pickedLink = null;
                    inPickingMode = false;
                }
            };

            GLThread.OnMouseWheel += (o, e) =>
            {
                if(e.Delta != 0 && inPickingMode)
                {
                    pickedLink.Offset.Z -= e.Delta * 3.0f;
                    if(pickedLink.Offset.Z > -5)
                        pickedLink.Offset.Z = -5;
                }
            };
            GLThread.OnMouseWheel += (o, e) =>
            {
                if(!inPickingMode)
                    Camera.Current.LensBlurAmount -= e.Delta / 20.0f;
            };
            World.Root.SimulationSpeed = 1.0f;

            GLThread.OnKeyDown += (o, e) =>
            {
                if(e.Key == OpenTK.Input.Key.T)
                {
                    World.Root.SimulationSpeed = 0.4f;
                }
                if(e.Key == OpenTK.Input.Key.Y)
                {
                    World.Root.SimulationSpeed = 0.10f;
                }

            };
            GLThread.OnKeyUp += (o, e) =>
            {
                if(e.Key == OpenTK.Input.Key.Tab)
                {
                    GLThread.DisplayAdapter.IsCursorVisible = !GLThread.DisplayAdapter.IsCursorVisible;
                }
               
                if(e.Key == OpenTK.Input.Key.Pause)
                {
                    ShaderProgram.RecompileAll();
                    ComputeShader.RecompileAll();
                }
                if(e.Key == OpenTK.Input.Key.LBracket)
                {
                    Camera.MainDisplayCamera.Brightness -= 0.1f;
                }
                if(e.Key == OpenTK.Input.Key.RBracket)
                {
                    Camera.MainDisplayCamera.Brightness += 0.1f;
                }
                if(e.Key == OpenTK.Input.Key.T)
                {
                    World.Root.SimulationSpeed = 1.0f;
                }
                if(e.Key == OpenTK.Input.Key.Y)
                {
                    World.Root.SimulationSpeed = 1.0f;
                }
                if(e.Key == OpenTK.Input.Key.Number1)
                {
                    //redConeLight.SetPosition(freeCamera.Cam.Transformation.GetPosition(), freeCamera.Cam.Transformation.GetPosition() + freeCamera.Cam.Transformation.GetOrientation().ToDirection());
                    RedLight.GetTransformationManager().SetPosition(Camera.MainDisplayCamera.Transformation.GetPosition());
                    RedLight.GetTransformationManager().SetOrientation(Camera.MainDisplayCamera.Transformation.GetOrientation());
                }
                if(e.Key == OpenTK.Input.Key.Tilde)
                {
                    Interpolator.Interpolate<Vector3>(RedLight.GetTransformationManager().Position, RedLight.GetTransformationManager().Position.R, Camera.MainDisplayCamera.GetPosition(), 8.0f, Interpolator.Easing.EaseInOut);
                }
                if(e.Key == OpenTK.Input.Key.Number0)
                    GLThread.GraphicsSettings.UseVDAO = !GLThread.GraphicsSettings.UseVDAO;
                if(e.Key == OpenTK.Input.Key.Number9)
                    GLThread.GraphicsSettings.UseBloom = !GLThread.GraphicsSettings.UseBloom;
                if(e.Key == OpenTK.Input.Key.Number8)
                    GLThread.GraphicsSettings.UseDeferred = !GLThread.GraphicsSettings.UseDeferred;
                if(e.Key == OpenTK.Input.Key.Number7)
                    GLThread.GraphicsSettings.UseDepth = !GLThread.GraphicsSettings.UseDepth;
                if(e.Key == OpenTK.Input.Key.Number6)
                    GLThread.GraphicsSettings.UseFog = !GLThread.GraphicsSettings.UseFog;
                if(e.Key == OpenTK.Input.Key.Number5)
                    GLThread.GraphicsSettings.UseLightPoints = !GLThread.GraphicsSettings.UseLightPoints;
                if(e.Key == OpenTK.Input.Key.Number4)
                    GLThread.GraphicsSettings.UseRSM = !GLThread.GraphicsSettings.UseRSM;
                if(e.Key == OpenTK.Input.Key.Number3)
                    GLThread.GraphicsSettings.UseSSReflections = !GLThread.GraphicsSettings.UseSSReflections;
                if(e.Key == OpenTK.Input.Key.Number2)
                    GLThread.GraphicsSettings.UseHBAO = !GLThread.GraphicsSettings.UseHBAO;
            };
        }
    }
}
