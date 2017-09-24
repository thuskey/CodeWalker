﻿using SharpDX.Direct3D11;
using SharpDX.DXGI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using MapFlags = SharpDX.Direct3D11.MapFlags;
using SharpDX;
using CodeWalker.GameFiles;
using CodeWalker.World;

namespace CodeWalker.Rendering
{

    public struct BasicShaderVSSceneVars
    {
        public Matrix ViewProj;
        public Vector4 WindVector;
    }
    public struct BasicShaderVSEntityVars
    {
        public Vector4 CamRel;
        public Quaternion Orientation;
        public uint HasSkeleton;
        public uint HasTransforms;
        public uint TintPaletteIndex;
        public uint Pad1;
        public Vector3 Scale;
        public uint IsInstanced;
    }
    public struct BasicShaderVSModelVars
    {
        public Matrix Transform;
    }
    public struct BasicShaderVSGeomVars
    {
        public uint EnableTint;
        public float TintYVal;
        public uint IsDecal;
        public uint EnableWind;
        public Vector4 WindOverrideParams;
    }
    public struct BasicShaderPSSceneVars
    {
        public ShaderGlobalLightParams GlobalLights;
        public uint EnableShadows;
        public uint RenderMode;//0=default, 1=normals, 2=tangents, 3=colours, 4=texcoords, 5=diffuse, 6=normalmap, 7=spec, 8=direct
        public uint RenderModeIndex; //colour/texcoord index
        public uint RenderSamplerCoord; //which texcoord to use in single texture mode
    }
    public struct BasicShaderPSGeomVars
    {
        public uint EnableTexture;
        public uint EnableTint;
        public uint EnableNormalMap;
        public uint EnableSpecMap;
        public uint EnableDetailMap;
        public uint IsDecal;
        public uint IsEmissive;
        public uint IsDistMap;
        public float bumpiness;
        public float AlphaScale;
        public float HardAlphaBlend;
        public float useTessellation;
        public Vector4 detailSettings;
        public Vector3 specMapIntMask;
        public float specularIntensityMult;
        public float specularFalloffMult;
        public float specularFresnel;
        public float wetnessMultiplier;
        public uint SpecOnly;
    }
    public struct BasicShaderInstGlobalMatrix
    {
        public Vector4 Row1;
        public Vector4 Row2;
        public Vector4 Row3;
    }
    public struct BasicShaderInstGlobals
    {
        public BasicShaderInstGlobalMatrix M0;
        public BasicShaderInstGlobalMatrix M1;
        public BasicShaderInstGlobalMatrix M2;
        public BasicShaderInstGlobalMatrix M3;
        public BasicShaderInstGlobalMatrix M4;
        public BasicShaderInstGlobalMatrix M5;
        public BasicShaderInstGlobalMatrix M6;
        public BasicShaderInstGlobalMatrix M7;
    }
    public struct BasicShaderInstLocals
    {
        public Vector3 vecBatchAabbMin;
        public float instPad0;
        public Vector3 vecBatchAabbDelta;
        public float instPad1;
        public Vector4 vecPlayerPos;
        public Vector2 _vecCollParams;
        public Vector2 instPad2;
        public Vector4 fadeAlphaDistUmTimer;
        public Vector4 uMovementParams;
        public Vector4 _fakedGrassNormal;
        public Vector3 gScaleRange;
        public float instPad3;
        public Vector4 gWindBendingGlobals;
        public Vector2 gWindBendScaleVar;
        public float gAlphaTest;
        public float gAlphaToCoverageScale;
        public Vector3 gLodFadeInstRange;
        public uint gUseComputeShaderOutputBuffer;
    }

    public class BasicShader : Shader, IDisposable
    {
        bool disposed = false;

        VertexShader basicvspnct;
        VertexShader basicvspncct;
        VertexShader basicvspncctt;
        VertexShader basicvspnccttt;
        VertexShader basicvspnctx;
        VertexShader basicvspncctx;
        VertexShader basicvspncttx;
        VertexShader basicvspnccttx;
        VertexShader basicvspnctttx;
        VertexShader basicvspncctttx;
        VertexShader basicvsbox;
        VertexShader basicvssphere;
        VertexShader basicvscapsule;
        VertexShader basicvscylinder;
        PixelShader basicps;
        GpuVarsBuffer<BasicShaderVSSceneVars> VSSceneVars;
        GpuVarsBuffer<BasicShaderVSEntityVars> VSEntityVars;
        GpuVarsBuffer<BasicShaderVSModelVars> VSModelVars;
        GpuVarsBuffer<BasicShaderVSGeomVars> VSGeomVars;
        GpuVarsBuffer<BasicShaderPSSceneVars> PSSceneVars;
        GpuVarsBuffer<BasicShaderPSGeomVars> PSGeomVars;
        GpuVarsBuffer<BasicShaderInstGlobals> InstGlobalVars;
        GpuVarsBuffer<BasicShaderInstLocals> InstLocalVars;
        SamplerState texsampler;
        SamplerState texsampleranis;
        SamplerState texsamplertnt;
        SamplerState texsamplertntyft;
        UnitCube cube; //for collision box render
        UnitSphere sphere; //for collision sphere render
        UnitCapsule capsule; //for collision capsule render
        UnitCylinder cylinder; //for collision cylinder render

        public bool AnisotropicFilter = false;
        public bool DecalMode = false;
        public float AlphaScale = 1.0f;
        public Vector4 WindVector = Vector4.Zero;
        public WorldRenderMode RenderMode = WorldRenderMode.Default;
        public int RenderVertexColourIndex = 1;
        public int RenderTextureCoordIndex = 1;
        public int RenderTextureSamplerCoord = 1;
        public MetaName RenderTextureSampler = MetaName.DiffuseSampler;
        public bool SpecularEnable = true;



        private Dictionary<VertexType, InputLayout> layouts = new Dictionary<VertexType, InputLayout>();

        public BasicShader(Device device)
        {
            byte[] vspnctbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCT.cso");
            byte[] vspncctbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCT.cso");
            byte[] vspnccttbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCTT.cso");
            byte[] vspncctttbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCTTT.cso");
            byte[] vspnctxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCTX.cso");
            byte[] vspncctxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCTX.cso");
            byte[] vspncttxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCTTX.cso");
            byte[] vspnccttxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCTTX.cso");
            byte[] vspnctttxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCTTTX.cso");
            byte[] vspncctttxbytes = File.ReadAllBytes("Shaders\\BasicVS_PNCCTTTX.cso");
            byte[] vsboxbytes = File.ReadAllBytes("Shaders\\BasicVS_Box.cso");
            byte[] vsspherebytes = File.ReadAllBytes("Shaders\\BasicVS_Sphere.cso");
            byte[] vscapsulebytes = File.ReadAllBytes("Shaders\\BasicVS_Capsule.cso");
            byte[] vscylinderbytes = File.ReadAllBytes("Shaders\\BasicVS_Cylinder.cso");
            byte[] psbytes = File.ReadAllBytes("Shaders\\BasicPS.cso");

            basicvspnct = new VertexShader(device, vspnctbytes);
            basicvspncct = new VertexShader(device, vspncctbytes);
            basicvspncctt = new VertexShader(device, vspnccttbytes);
            basicvspnccttt = new VertexShader(device, vspncctttbytes);
            basicvspnctx = new VertexShader(device, vspnctxbytes);
            basicvspncctx = new VertexShader(device, vspncctxbytes);
            basicvspncttx = new VertexShader(device, vspncttxbytes);
            basicvspnccttx = new VertexShader(device, vspnccttxbytes);
            basicvspnctttx = new VertexShader(device, vspnctttxbytes);
            basicvspncctttx = new VertexShader(device, vspncctttxbytes);
            basicvsbox = new VertexShader(device, vsboxbytes);
            basicvssphere = new VertexShader(device, vsspherebytes);
            basicvscapsule = new VertexShader(device, vscapsulebytes);
            basicvscylinder = new VertexShader(device, vscylinderbytes);
            basicps = new PixelShader(device, psbytes);

            VSSceneVars = new GpuVarsBuffer<BasicShaderVSSceneVars>(device);
            VSEntityVars = new GpuVarsBuffer<BasicShaderVSEntityVars>(device);
            VSModelVars = new GpuVarsBuffer<BasicShaderVSModelVars>(device);
            VSGeomVars = new GpuVarsBuffer<BasicShaderVSGeomVars>(device);
            PSSceneVars = new GpuVarsBuffer<BasicShaderPSSceneVars>(device);
            PSGeomVars = new GpuVarsBuffer<BasicShaderPSGeomVars>(device);
            InstGlobalVars = new GpuVarsBuffer<BasicShaderInstGlobals>(device);
            InstLocalVars = new GpuVarsBuffer<BasicShaderInstLocals>(device);

            InitInstGlobalVars();


            //supported layouts - requires Position, Normal, Colour, Texcoord
            layouts.Add(VertexType.Default, new InputLayout(device, vspnctbytes, VertexTypeDefault.GetLayout()));
            layouts.Add(VertexType.PNCH2, new InputLayout(device, vspnctbytes, VertexTypePNCH2.GetLayout()));

            layouts.Add(VertexType.PCCNCT, new InputLayout(device, vspncctbytes, VertexTypePCCNCT.GetLayout()));
            layouts.Add(VertexType.PCCNCCT, new InputLayout(device, vspncctbytes, VertexTypePCCNCCT.GetLayout()));
            layouts.Add(VertexType.PNCCT, new InputLayout(device, vspncctbytes, VertexTypePNCCT.GetLayout()));
            layouts.Add(VertexType.PNCCTT, new InputLayout(device, vspnccttbytes, VertexTypePNCCTT.GetLayout()));
            layouts.Add(VertexType.PNCCTTTT, new InputLayout(device, vspncctttbytes, VertexTypePNCCTTTT.GetLayout()));


            //normalmap layouts - requires Position, Normal, Colour, Texcoord, Tangent (X)
            layouts.Add(VertexType.DefaultEx, new InputLayout(device, vspnctxbytes, VertexTypeDefaultEx.GetLayout()));
            layouts.Add(VertexType.PCCH2H4, new InputLayout(device, vspnctxbytes, VertexTypePCCH2H4.GetLayout()));

            layouts.Add(VertexType.PCCNCTX, new InputLayout(device, vspncctxbytes, VertexTypePCCNCTX.GetLayout()));
            layouts.Add(VertexType.PCCNCCTX, new InputLayout(device, vspncctxbytes, VertexTypePCCNCCTX.GetLayout()));
            layouts.Add(VertexType.PNCCTX, new InputLayout(device, vspncctxbytes, VertexTypePNCCTX.GetLayout()));
            layouts.Add(VertexType.PNCTTX, new InputLayout(device, vspncttxbytes, VertexTypePNCTTX.GetLayout()));
            layouts.Add(VertexType.PNCCTTX, new InputLayout(device, vspnccttxbytes, VertexTypePNCCTTX.GetLayout()));
            layouts.Add(VertexType.PNCCTTX_2, new InputLayout(device, vspnccttxbytes, VertexTypePNCCTTX_2.GetLayout()));
            layouts.Add(VertexType.PCCNCCTTX, new InputLayout(device, vspnccttxbytes, VertexTypePCCNCCTTX.GetLayout()));
            layouts.Add(VertexType.PNCTTTX, new InputLayout(device, vspnctttxbytes, VertexTypePNCTTTX.GetLayout()));
            layouts.Add(VertexType.PNCTTTX_2, new InputLayout(device, vspnctttxbytes, VertexTypePNCTTTX_2.GetLayout()));
            layouts.Add(VertexType.PNCTTTX_3, new InputLayout(device, vspnctttxbytes, VertexTypePNCTTTX_3.GetLayout()));
            layouts.Add(VertexType.PNCTTTTX, new InputLayout(device, vspnctttxbytes, VertexTypePNCTTTTX.GetLayout()));
            layouts.Add(VertexType.PNCCTTTX, new InputLayout(device, vspncctttxbytes, VertexTypePNCCTTTX.GetLayout()));


            layouts.Add(VertexType.PCCNCTT, new InputLayout(device, vspnccttbytes, VertexTypePCCNCTT.GetLayout()));
            layouts.Add(VertexType.PCCNCTTX, new InputLayout(device, vspnccttxbytes, VertexTypePCCNCTTX.GetLayout()));
            layouts.Add(VertexType.PCCNCTTT, new InputLayout(device, vspncctttbytes, VertexTypePCCNCTTT.GetLayout()));
            layouts.Add(VertexType.PNCTT, new InputLayout(device, vspnctbytes, VertexTypePNCTT.GetLayout()));
            layouts.Add(VertexType.PNCTTT, new InputLayout(device, vspnctbytes, VertexTypePNCTTT.GetLayout()));



            texsampler = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipLinear,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
            texsampleranis = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.Anisotropic,
                MaximumAnisotropy = 8,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
            texsamplertnt = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Clamp,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.White,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipPoint,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });
            texsamplertntyft = new SamplerState(device, new SamplerStateDescription()
            {
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.White,
                ComparisonFunction = Comparison.Always,
                Filter = Filter.MinMagMipPoint,
                MaximumAnisotropy = 1,
                MaximumLod = float.MaxValue,
                MinimumLod = 0,
                MipLodBias = 0,
            });


            cube = new UnitCube(device, vsboxbytes, false, false, true);
            sphere = new UnitSphere(device, vsspherebytes, 4);
            capsule = new UnitCapsule(device, vscapsulebytes, 4);
            cylinder = new UnitCylinder(device, vscylinderbytes, 8);
        }

        private void InitInstGlobalVars()
        {
            var m0 = Matrix3x3.RotationZ(0.00f * (float)Math.PI);
            var m1 = Matrix3x3.RotationZ(0.25f * (float)Math.PI);
            var m2 = Matrix3x3.RotationZ(0.50f * (float)Math.PI);
            var m3 = Matrix3x3.RotationZ(0.75f * (float)Math.PI);
            var m4 = Matrix3x3.RotationZ(1.00f * (float)Math.PI);
            var m5 = Matrix3x3.RotationZ(1.25f * (float)Math.PI);
            var m6 = Matrix3x3.RotationZ(1.50f * (float)Math.PI);
            var m7 = Matrix3x3.RotationZ(1.75f * (float)Math.PI);

            InstGlobalVars.Vars.M0.Row1 = new Vector4(m0.Row1, 1);
            InstGlobalVars.Vars.M0.Row2 = new Vector4(m0.Row2, 1);
            InstGlobalVars.Vars.M0.Row3 = new Vector4(m0.Row3, 1);
            InstGlobalVars.Vars.M1.Row1 = new Vector4(m1.Row1, 1);
            InstGlobalVars.Vars.M1.Row2 = new Vector4(m1.Row2, 1);
            InstGlobalVars.Vars.M1.Row3 = new Vector4(m1.Row3, 1);
            InstGlobalVars.Vars.M2.Row1 = new Vector4(m2.Row1, 1);
            InstGlobalVars.Vars.M2.Row2 = new Vector4(m2.Row2, 1);
            InstGlobalVars.Vars.M2.Row3 = new Vector4(m2.Row3, 1);
            InstGlobalVars.Vars.M3.Row1 = new Vector4(m3.Row1, 1);
            InstGlobalVars.Vars.M3.Row2 = new Vector4(m3.Row2, 1);
            InstGlobalVars.Vars.M3.Row3 = new Vector4(m3.Row3, 1);
            InstGlobalVars.Vars.M4.Row1 = new Vector4(m4.Row1, 1);
            InstGlobalVars.Vars.M4.Row2 = new Vector4(m4.Row2, 1);
            InstGlobalVars.Vars.M4.Row3 = new Vector4(m4.Row3, 1);
            InstGlobalVars.Vars.M5.Row1 = new Vector4(m5.Row1, 1);
            InstGlobalVars.Vars.M5.Row2 = new Vector4(m5.Row2, 1);
            InstGlobalVars.Vars.M5.Row3 = new Vector4(m5.Row3, 1);
            InstGlobalVars.Vars.M6.Row1 = new Vector4(m6.Row1, 1);
            InstGlobalVars.Vars.M6.Row2 = new Vector4(m6.Row2, 1);
            InstGlobalVars.Vars.M6.Row3 = new Vector4(m6.Row3, 1);
            InstGlobalVars.Vars.M7.Row1 = new Vector4(m7.Row1, 1);
            InstGlobalVars.Vars.M7.Row2 = new Vector4(m7.Row2, 1);
            InstGlobalVars.Vars.M7.Row3 = new Vector4(m7.Row3, 1);

        }


        private void SetVertexShader(DeviceContext context, VertexType type)
        {
            VertexShader vs = basicvspnct;
            switch (type)
            {
                case VertexType.Default:
                case VertexType.PNCH2:
                case VertexType.PNCTT:
                case VertexType.PNCTTT:
                    vs = basicvspnct;
                    break;
                case VertexType.PCCNCT:
                case VertexType.PCCNCCT:
                case VertexType.PNCCT:
                    vs = basicvspncct;
                    break;
                case VertexType.PNCCTT://not used?
                case VertexType.PCCNCTT:
                    vs = basicvspncctt;
                    break;
                case VertexType.PNCCTTTT://not used?
                case VertexType.PCCNCTTT:
                    vs = basicvspnccttt;
                    break;
                case VertexType.DefaultEx:
                case VertexType.PCCH2H4:
                    vs = basicvspnctx;
                    break;

                case VertexType.PCCNCTX:
                case VertexType.PCCNCCTX:
                case VertexType.PNCCTX:
                    vs = basicvspncctx;
                    break;

                case VertexType.PNCTTX:
                    vs = basicvspncttx;
                    break;

                case VertexType.PNCCTTX://not used?
                case VertexType.PNCCTTX_2://not used?
                case VertexType.PCCNCCTTX://not used?
                case VertexType.PCCNCTTX:
                    vs = basicvspnccttx;
                    break;

                case VertexType.PNCTTTX:
                case VertexType.PNCTTTX_2:
                case VertexType.PNCTTTX_3:
                case VertexType.PNCTTTTX: //not using last texcoords!
                    vs = basicvspnctttx;
                    break;

                case VertexType.PNCCTTTX://not used?
                    vs = basicvspncctttx;
                    break;

                default:
                    break;

            }
            context.VertexShader.Set(vs);
        }


        public override void SetShader(DeviceContext context)
        {
            context.PixelShader.Set(basicps);
        }

        public override bool SetInputLayout(DeviceContext context, VertexType type)
        {
            InputLayout l;
            if (layouts.TryGetValue(type, out l))
            {
                SetVertexShader(context, type);
                context.InputAssembler.InputLayout = l;
                return true;
            }
            return false;
        }

        public override void SetSceneVars(DeviceContext context, Camera camera, Shadowmap shadowmap, ShaderGlobalLights lights)
        {
            uint rendermode = 0;
            uint rendermodeind = 1;

            SpecularEnable = lights.SpecularEnabled;

            switch (RenderMode)
            {
                case WorldRenderMode.VertexNormals:
                    rendermode = 1;
                    break;
                case WorldRenderMode.VertexTangents:
                    rendermode = 2;
                    break;
                case WorldRenderMode.VertexColour:
                    rendermode = 3;
                    rendermodeind = (uint)RenderVertexColourIndex;
                    break;
                case WorldRenderMode.TextureCoord:
                    rendermode = 4;
                    rendermodeind = (uint)RenderTextureCoordIndex;
                    break;
                case WorldRenderMode.SingleTexture:
                    rendermode = 8;//direct mode
                    break;
            }


            VSSceneVars.Vars.ViewProj = Matrix.Transpose(camera.ViewProjMatrix);
            VSSceneVars.Vars.WindVector = WindVector;
            VSSceneVars.Update(context);
            VSSceneVars.SetVSCBuffer(context, 0);

            PSSceneVars.Vars.GlobalLights = lights.Params;
            PSSceneVars.Vars.EnableShadows = (shadowmap != null) ? 1u : 0u;
            PSSceneVars.Vars.RenderMode = rendermode;
            PSSceneVars.Vars.RenderModeIndex = rendermodeind;
            PSSceneVars.Vars.RenderSamplerCoord = (uint)RenderTextureSamplerCoord;
            PSSceneVars.Update(context);
            PSSceneVars.SetPSCBuffer(context, 0);

            if (shadowmap != null)
            {
                shadowmap.SetFinalRenderResources(context);
            }

            if (!InstGlobalVars.Flag) //on the first frame, update the instance globals
            {
                InstGlobalVars.Update(context);
                InstGlobalVars.Flag = true;
            }

        }

        public override void SetEntityVars(DeviceContext context, ref RenderableInst rend)
        {
            VSEntityVars.Vars.CamRel = new Vector4(rend.CamRel, 0.0f);
            VSEntityVars.Vars.Orientation = rend.Orientation;
            VSEntityVars.Vars.Scale = rend.Scale;
            VSEntityVars.Vars.HasSkeleton = rend.Renderable.HasSkeleton ? 1u : 0;
            VSEntityVars.Vars.HasTransforms = rend.Renderable.HasTransforms ? 1u : 0;
            VSEntityVars.Vars.TintPaletteIndex = rend.TintPaletteIndex;
            VSEntityVars.Vars.IsInstanced = 0;
            VSEntityVars.Update(context);
            VSEntityVars.SetVSCBuffer(context, 2);
        }

        public override void SetModelVars(DeviceContext context, RenderableModel model)
        {
            if (!model.UseTransform) return;
            VSModelVars.Vars.Transform = Matrix.Transpose(model.Transform);
            VSModelVars.Update(context);
            VSModelVars.SetVSCBuffer(context, 3);
        }

        public override void SetGeomVars(DeviceContext context, RenderableGeometry geom)
        {
            RenderableTexture texture = null;
            RenderableTexture tintpal = null;
            RenderableTexture bumptex = null;
            RenderableTexture spectex = null;
            RenderableTexture detltex = null;
            bool isdistmap = false;

            float tntpalind = 0.0f;
            if ((geom.RenderableTextures != null) && (geom.RenderableTextures.Length > 0))
            {
                if (RenderMode == WorldRenderMode.Default)
                {
                    for (int i = 0; i < geom.RenderableTextures.Length; i++)
                    {
                        var itex = geom.RenderableTextures[i];
                        var ihash = geom.TextureParamHashes[i];
                        if (itex == null) continue;
                        switch (ihash)
                        {
                            case MetaName.DiffuseSampler:
                                texture = itex;
                                break;
                            case MetaName.BumpSampler:
                                bumptex = itex;
                                break;
                            case MetaName.SpecSampler:
                                spectex = itex;
                                break;
                            case MetaName.DetailSampler:
                                detltex = itex;
                                break;
                            case MetaName.TintPaletteSampler:
                                tintpal = itex;
                                if (tintpal.Key != null)
                                {
                                    //this is slightly dodgy but VSEntityVars should have the correct value in it...
                                    tntpalind = (VSEntityVars.Vars.TintPaletteIndex + 0.5f) / tintpal.Key.Height;
                                }
                                break;
                            case MetaName.distanceMapSampler:
                                texture = itex;
                                isdistmap = true;
                                break;
                            case MetaName.heightSampler:
                            case MetaName.EnvironmentSampler:
                                break;
                            case MetaName.FlowSampler:
                            case MetaName.FogSampler:
                            case MetaName.FoamSampler:
                                if (texture == null) texture = itex;
                                break;
                            default:
                                if (texture == null) texture = itex;
                                break;
                        }
                    }
                }
                else if (RenderMode == WorldRenderMode.SingleTexture)
                {
                    for (int i = 0; i < geom.RenderableTextures.Length; i++)
                    {
                        var itex = geom.RenderableTextures[i];
                        var ihash = geom.TextureParamHashes[i];
                        if (ihash == RenderTextureSampler)
                        {
                            texture = itex;
                            break;
                        }
                    }
                }
            }


            bool usediff = ((texture != null) && (texture.ShaderResourceView != null));
            bool usebump = ((bumptex != null) && (bumptex.ShaderResourceView != null));
            bool usespec = ((spectex != null) && (spectex.ShaderResourceView != null));
            bool usedetl = ((detltex != null) && (detltex.ShaderResourceView != null));
            bool usetint = ((tintpal != null) && (tintpal.ShaderResourceView != null));

            uint tintflag = 0;
            if (usetint) tintflag = 1;

            uint windflag = geom.EnableWind ? 1u : 0u;
            uint emflag = geom.IsEmissive ? 1u : 0u;
            var shaderName = geom.DrawableGeom.Shader.Name;
            var shaderFile = geom.DrawableGeom.Shader.FileName;
            switch (shaderFile.Hash)
            {
                case 2245870123: //trees_normal_diffspec_tnt.sps
                case 3334613197: //trees_tnt.sps
                case 1229591973://{trees_normal_spec_tnt.sps}
                    if (usetint) tintflag = 2; //use 2nd vertex colour channel for tint...
                    break;
                case 3880384844://{decal_spec_only.sps}w
                case 600733812://{decal_amb_only.sps}
                case 2842248626://{spec_decal.sps}
                case 2457676400://{reflect_decal.sps}
                case 2706821972://{mirror_decal.sps}
                    //if (RenderMode == WorldRenderMode.Default) usediff = false;
                    break;
            }

            uint pstintflag = tintflag;
            if (VSEntityVars.Vars.IsInstanced>0)
            {
                pstintflag = 1;
                switch (shaderFile.Hash)
                {
                    case 916743331: //{grass_batch.sps}
                        windflag = 1;
                        break;
                    case 3833671083://{normal_spec_batch.sps}
                        windflag = 0;
                        break;
                    default:
                        break;
                }
            }


            PSGeomVars.Vars.EnableTexture = usediff ? 1u : 0u;
            PSGeomVars.Vars.EnableTint = pstintflag;
            PSGeomVars.Vars.EnableNormalMap = usebump ? 1u : 0u;
            PSGeomVars.Vars.EnableSpecMap = usespec ? 1u : 0u;
            PSGeomVars.Vars.EnableDetailMap = usedetl ? 1u : 0u;
            PSGeomVars.Vars.IsDecal = DecalMode ? 1u : 0u;
            PSGeomVars.Vars.IsEmissive = emflag;
            PSGeomVars.Vars.IsDistMap = isdistmap ? 1u : 0u;
            PSGeomVars.Vars.bumpiness = geom.bumpiness;
            PSGeomVars.Vars.AlphaScale = isdistmap ? 1.0f : AlphaScale;
            PSGeomVars.Vars.HardAlphaBlend = 0.0f; //todo: cutouts flag!
            PSGeomVars.Vars.useTessellation = 0.0f;
            PSGeomVars.Vars.detailSettings = geom.detailSettings;
            PSGeomVars.Vars.specMapIntMask = geom.specMapIntMask;
            PSGeomVars.Vars.specularIntensityMult = SpecularEnable ? geom.specularIntensityMult : 0.0f;
            PSGeomVars.Vars.specularFalloffMult = geom.specularFalloffMult;
            PSGeomVars.Vars.specularFresnel = geom.specularFresnel;
            PSGeomVars.Vars.wetnessMultiplier = geom.wetnessMultiplier;
            PSGeomVars.Vars.SpecOnly = geom.SpecOnly ? 1u : 0u;
            PSGeomVars.Update(context);
            PSGeomVars.SetPSCBuffer(context, 2);

            VSGeomVars.Vars.EnableTint = tintflag;
            VSGeomVars.Vars.TintYVal = tntpalind;
            VSGeomVars.Vars.IsDecal = DecalMode ? 1u : 0u;
            VSGeomVars.Vars.EnableWind = windflag;
            VSGeomVars.Vars.WindOverrideParams = geom.WindOverrideParams;
            VSGeomVars.Update(context);
            VSGeomVars.SetVSCBuffer(context, 4);

            context.VertexShader.SetSampler(0, geom.IsFragment ? texsamplertntyft : texsamplertnt);
            context.PixelShader.SetSampler(0, AnisotropicFilter ? texsampleranis : texsampler);
            if (usediff)
            {
                texture.SetPSResource(context, 0);
            }
            if (usebump)
            {
                bumptex.SetPSResource(context, 2);
            }
            if (usespec)
            {
                spectex.SetPSResource(context, 3);
            }
            if (usedetl)
            {
                detltex.SetPSResource(context, 4);
            }
            if (usetint)
            {
                tintpal.SetVSResource(context, 0);
            }
        }


        public void SetInstanceVars(DeviceContext context, RenderableInstanceBatch batch)
        {
            var gb = batch.Key;

            VSEntityVars.Vars.CamRel = new Vector4(gb.CamRel, 0.0f);
            VSEntityVars.Vars.Orientation = Quaternion.Identity;
            VSEntityVars.Vars.Scale = Vector3.One;
            VSEntityVars.Vars.HasSkeleton = 0;
            VSEntityVars.Vars.HasTransforms = 0;
            VSEntityVars.Vars.TintPaletteIndex = 0;
            VSEntityVars.Vars.IsInstanced = 1;
            VSEntityVars.Update(context);
            VSEntityVars.SetVSCBuffer(context, 2);

            InstGlobalVars.SetVSCBuffer(context, 5);

            InstLocalVars.Vars.vecBatchAabbMin = gb.AABBMin;
            InstLocalVars.Vars.vecBatchAabbDelta = gb.AABBMax - gb.AABBMin;
            InstLocalVars.Vars.vecPlayerPos = new Vector4(gb.Position - gb.CamRel, 1.0f);
            InstLocalVars.Vars._vecCollParams = new Vector2(2.0f, -3.0f);//range, offset
            InstLocalVars.Vars.fadeAlphaDistUmTimer = new Vector4(0.0f);
            InstLocalVars.Vars.uMovementParams = new Vector4(0.0f);
            InstLocalVars.Vars._fakedGrassNormal = new Vector4(Vector3.Normalize(-gb.CamRel), 0.0f);
            InstLocalVars.Vars.gScaleRange = gb.Batch.ScaleRange;
            InstLocalVars.Vars.gWindBendingGlobals = new Vector4(WindVector.X, WindVector.Y, 1.0f, 1.0f);
            InstLocalVars.Vars.gWindBendScaleVar = new Vector2(WindVector.Z, WindVector.W);
            InstLocalVars.Vars.gAlphaTest = 0.0f;
            InstLocalVars.Vars.gAlphaToCoverageScale = 1.0f;
            InstLocalVars.Vars.gLodFadeInstRange = new Vector3(gb.Batch.LodInstFadeRange, gb.Batch.LodFadeStartDist, gb.Batch.lodDist);
            InstLocalVars.Vars.gUseComputeShaderOutputBuffer = 0;
            InstLocalVars.Update(context);
            InstLocalVars.SetVSCBuffer(context, 6);


            context.VertexShader.SetShaderResource(2, batch.GrassInstanceBuffer.SRV);
        }


        public void RenderBoundGeom(DeviceContext context, RenderableBoundGeometryInst inst)
        {


            VSEntityVars.Vars.CamRel = new Vector4(inst.Inst.CamRel, 0.0f);
            VSEntityVars.Vars.Orientation = inst.Inst.Orientation;
            VSEntityVars.Vars.Scale = inst.Inst.Scale;
            VSEntityVars.Vars.HasSkeleton = 0;
            VSEntityVars.Vars.HasTransforms = 0; //todo! bounds transforms..?
            VSEntityVars.Vars.TintPaletteIndex = 0;
            VSEntityVars.Vars.IsInstanced = 0;
            VSEntityVars.Update(context);
            VSEntityVars.SetVSCBuffer(context, 2);

            PSGeomVars.Vars.EnableTexture = 0;
            PSGeomVars.Vars.EnableTint = 0;
            PSGeomVars.Vars.EnableNormalMap = 0;
            PSGeomVars.Vars.EnableSpecMap = 0;
            PSGeomVars.Vars.EnableDetailMap = 0;
            PSGeomVars.Vars.IsDecal = 0;
            PSGeomVars.Vars.IsEmissive = 0;
            PSGeomVars.Vars.IsDistMap = 0;
            PSGeomVars.Vars.bumpiness = 0;
            PSGeomVars.Vars.AlphaScale = 1;
            PSGeomVars.Vars.HardAlphaBlend = 0;
            PSGeomVars.Vars.useTessellation = 0;
            PSGeomVars.Vars.detailSettings = Vector4.Zero;
            PSGeomVars.Vars.specMapIntMask = Vector3.Zero;
            PSGeomVars.Vars.specularIntensityMult = 1.0f;
            PSGeomVars.Vars.specularFalloffMult = 1.0f;
            PSGeomVars.Vars.specularFresnel = 1.0f;
            PSGeomVars.Vars.wetnessMultiplier = 0.0f;
            PSGeomVars.Vars.SpecOnly = 0;
            PSGeomVars.Update(context);
            PSGeomVars.SetPSCBuffer(context, 2);

            VSGeomVars.Vars.EnableTint = 0;
            VSGeomVars.Vars.TintYVal = 0.0f;
            VSGeomVars.Vars.IsDecal = 0;
            VSGeomVars.Vars.EnableWind = 0;
            VSGeomVars.Vars.WindOverrideParams = Vector4.Zero;
            VSGeomVars.Update(context);
            VSGeomVars.SetVSCBuffer(context, 4);


            if (inst.Geom.VertexBuffer != null) //render the triangles
            {
                SetVertexShader(context, VertexType.Default);
                SetInputLayout(context, VertexType.Default);
                inst.Geom.RenderTriangles(context);
            }

            //render the boxes
            if (inst.Geom.BoxBuffer != null)
            {
                context.VertexShader.Set(basicvsbox);
                context.VertexShader.SetShaderResource(1, inst.Geom.BoxBuffer.SRV);
                cube.DrawInstanced(context, inst.Geom.BoxBuffer.StructCount);
            }

            //render the spheres
            if (inst.Geom.SphereBuffer != null)
            {
                context.VertexShader.Set(basicvssphere);
                context.VertexShader.SetShaderResource(1, inst.Geom.SphereBuffer.SRV);
                sphere.DrawInstanced(context, inst.Geom.SphereBuffer.StructCount);
            }

            //render the capsules
            if (inst.Geom.CapsuleBuffer != null)
            {
                context.VertexShader.Set(basicvscapsule);
                context.VertexShader.SetShaderResource(1, inst.Geom.CapsuleBuffer.SRV);
                capsule.DrawInstanced(context, inst.Geom.CapsuleBuffer.StructCount);
            }

            //render the cylinders
            if (inst.Geom.CylinderBuffer != null)
            {
                context.VertexShader.Set(basicvscylinder);
                context.VertexShader.SetShaderResource(1, inst.Geom.CylinderBuffer.SRV);
                cylinder.DrawInstanced(context, inst.Geom.CylinderBuffer.StructCount);
            }


        }


        public override void UnbindResources(DeviceContext context)
        {
            context.VertexShader.SetConstantBuffer(0, null);
            context.PixelShader.SetConstantBuffer(0, null);
            context.VertexShader.SetConstantBuffer(1, null); //shadowmap
            context.PixelShader.SetConstantBuffer(1, null); //shadowmap
            context.PixelShader.SetShaderResource(1, null);//shadowmap
            context.PixelShader.SetSampler(1, null); //shadowmap
            context.VertexShader.SetConstantBuffer(2, null);
            context.VertexShader.SetConstantBuffer(3, null);
            context.PixelShader.SetConstantBuffer(2, null);
            context.VertexShader.SetConstantBuffer(4, null);
            context.VertexShader.SetConstantBuffer(5, null);
            context.VertexShader.SetConstantBuffer(6, null);
            context.VertexShader.SetSampler(0, null);
            context.PixelShader.SetSampler(0, null);
            context.PixelShader.SetShaderResource(0, null);
            context.PixelShader.SetShaderResource(2, null);
            context.PixelShader.SetShaderResource(3, null);
            context.PixelShader.SetShaderResource(4, null);
            context.VertexShader.SetShaderResource(0, null);
            context.VertexShader.SetShaderResource(1, null);
            context.VertexShader.SetShaderResource(2, null);
            context.VertexShader.Set(null);
            context.PixelShader.Set(null);
        }


        public void Dispose()
        {
            if (disposed) return;

            cube.Dispose();
            sphere.Dispose();
            capsule.Dispose();
            cylinder.Dispose();

            texsampler.Dispose();
            texsampleranis.Dispose();
            texsamplertnt.Dispose();

            foreach (InputLayout layout in layouts.Values)
            {
                layout.Dispose();
            }
            layouts.Clear();

            VSSceneVars.Dispose();
            VSEntityVars.Dispose();
            VSModelVars.Dispose();
            VSGeomVars.Dispose();
            PSSceneVars.Dispose();
            PSGeomVars.Dispose();
            InstGlobalVars.Dispose();
            InstLocalVars.Dispose();

            basicps.Dispose();
            basicvspnct.Dispose();
            basicvspncct.Dispose();
            basicvspncctt.Dispose();
            basicvspnccttt.Dispose();
            basicvspnctx.Dispose();
            basicvspncctx.Dispose();
            basicvspncttx.Dispose();
            basicvspnccttx.Dispose();
            basicvspnctttx.Dispose();
            basicvspncctttx.Dispose();
            basicvsbox.Dispose();
            basicvssphere.Dispose();
            basicvscapsule.Dispose();
            basicvscylinder.Dispose();

            disposed = true;
        }
    }
}