using System;
using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace MultiSignpost.Blocks.EntityMultiSignPost
{
    public sealed class MeshFactory
    {
        private const float MinimumPoleHalfWidth = 0.03125f;
        private const float MaximumPoleHalfWidth = 0.5f;
        private const float SelectionBoxInflation = 0.002f;

        private readonly BlockPos basePos;
        private readonly Func<List<string>[]> getTextByDirection;
        private readonly Func<float> getScale;

        public MeshFactory(
            BlockPos basePos,
            Func<List<string>[]> getTextByDirection,
            Func<float> getScale)
        {
            this.basePos = basePos?.Copy() ?? throw new ArgumentNullException(nameof(basePos));
            this.getTextByDirection = getTextByDirection ?? throw new ArgumentNullException(nameof(getTextByDirection));
            this.getScale = getScale ?? throw new ArgumentNullException(nameof(getScale));
        }

        public MeshData GetBasePoleBlockModelMesh()
        {
            return CreateCubeMeshFromBoxes(GetBasePoleBoxes());
        }

        public MeshData GetExtensionPoleBlockModelMesh(BlockPos extensionPos)
        {
            return CreateCubeMeshFromBoxes(GetExtensionPoleBoxes(extensionPos));
        }

        public MeshData GetBasePoleDecalMesh(ITexPositionSource decalTexSource)
        {
            MeshData mesh = CreateCubeMeshFromBoxes(GetBasePoleBoxes());
            ApplyDecalTexture(mesh, decalTexSource);

            return mesh;
        }

        public MeshData GetExtensionPoleDecalMesh(BlockPos extensionPos, ITexPositionSource decalTexSource)
        {
            MeshData mesh = CreateCubeMeshFromBoxes(GetExtensionPoleBoxes(extensionPos));
            ApplyDecalTexture(mesh, decalTexSource);

            return mesh;
        }

        public Cuboidf[] GetBasePoleBoxes()
        {
            float scale = GetCurrentScale();
            float visualHeight = GetCurrentVisualHeight(scale);

            return CreatePoleBoxes(
                scale,
                visualHeight,
                0f,
                Constants.BaseOccupiedHeightBlocks
            );
        }

        public Cuboidf[] GetExtensionPoleBoxes(BlockPos extensionPos)
        {
            if (extensionPos == null)
            {
                return Array.Empty<Cuboidf>();
            }

            float scale = GetCurrentScale();
            float visualHeight = GetCurrentVisualHeight(scale);
            float segmentStartY = extensionPos.Y - basePos.Y;

            return CreatePoleBoxes(
                scale,
                visualHeight,
                segmentStartY,
                1f
            );
        }

        public Cuboidf[] GetBasePoleSelectionBoxes()
        {
            return InflateSelectionBoxes(GetBasePoleBoxes());
        }

        public Cuboidf[] GetExtensionPoleSelectionBoxes(BlockPos extensionPos)
        {
            return InflateSelectionBoxes(GetExtensionPoleBoxes(extensionPos));
        }

        public Cuboidf GetBasePoleParticleBreakBox()
        {
            return FirstOrDefaultBox(GetBasePoleBoxes());
        }

        public Cuboidf GetExtensionPoleParticleBreakBox(BlockPos extensionPos)
        {
            return FirstOrDefaultBox(GetExtensionPoleBoxes(extensionPos));
        }

        private float GetCurrentScale()
        {
            return Math.Max(0.01f, getScale());
        }

        private float GetCurrentVisualHeight(float scale)
        {
            List<string>[] textByDirection = getTextByDirection();

            if (textByDirection == null)
            {
                return 0f;
            }

            return Geometry.GetRequiredVisualHeight(textByDirection, scale);
        }

        private static MeshData CreateCubeMeshFromBoxes(Cuboidf[] boxes)
        {
            MeshData result = new MeshData(24, 36);

            if (boxes == null)
            {
                return result;
            }

            foreach (Cuboidf box in boxes)
            {
                float scaleH = (box.X2 - box.X1) / 2f;
                float scaleV = (box.Y2 - box.Y1) / 2f;

                if (scaleH <= 0 || scaleV <= 0)
                {
                    continue;
                }

                Vec3f center = new Vec3f(
                    (box.X1 + box.X2) / 2f,
                    (box.Y1 + box.Y2) / 2f,
                    (box.Z1 + box.Z2) / 2f
                );

                MeshData cube = CubeMeshUtil.GetCubeOnlyScaleXyz(scaleH, scaleV, center);
                cube.Rgba.Fill((byte)255);

                result.AddMeshData(cube);
            }

            return result;
        }

        private static void ApplyDecalTexture(MeshData mesh, ITexPositionSource decalTexSource)
        {
            TextureAtlasPosition texPos = GetDecalTexturePosition(decalTexSource);

            if (texPos == null || mesh?.Uv == null)
            {
                return;
            }

            float width = texPos.x2 - texPos.x1;
            float height = texPos.y2 - texPos.y1;

            for (int i = 0; i < mesh.VerticesCount; i++)
            {
                int uvIndex = i * 2;

                float u = mesh.Uv[uvIndex];
                float v = mesh.Uv[uvIndex + 1];

                mesh.Uv[uvIndex] = texPos.x1 + width * u;
                mesh.Uv[uvIndex + 1] = texPos.y1 + height * v;
            }
        }

        private static TextureAtlasPosition GetDecalTexturePosition(ITexPositionSource decalTexSource)
        {
            if (decalTexSource == null)
            {
                return null;
            }

            string[] textureCodes =
            {
            "sign",
            "all",
            "crack",
            "decal"
        };

            foreach (string textureCode in textureCodes)
            {
                try
                {
                    TextureAtlasPosition texPos = decalTexSource[textureCode];

                    if (texPos != null)
                    {
                        return texPos;
                    }
                }
                catch
                {
                    // Try the next likely texture code.
                }
            }

            return null;
        }

        private static Cuboidf[] CreatePoleBoxes(
            float scale,
            float visualHeight,
            float segmentStartY,
            float segmentHeight)
        {
            scale = Math.Max(0.01f, scale);

            float localY2 = Math.Min(segmentHeight, visualHeight - segmentStartY);

            if (localY2 <= Constants.HeightEpsilon)
            {
                return Array.Empty<Cuboidf>();
            }

            float halfWidth = Constants.PoleHalfWidth * scale;
            halfWidth = Math.Max(MinimumPoleHalfWidth, Math.Min(MaximumPoleHalfWidth, halfWidth));

            return new[]
            {
            new Cuboidf(
                0.5f - halfWidth,
                0f,
                0.5f - halfWidth,
                0.5f + halfWidth,
                localY2,
                0.5f + halfWidth
            )
        };
        }

        private static Cuboidf[] InflateSelectionBoxes(Cuboidf[] boxes)
        {
            if (boxes == null || boxes.Length == 0)
            {
                return boxes;
            }

            Cuboidf[] result = new Cuboidf[boxes.Length];

            for (int i = 0; i < boxes.Length; i++)
            {
                Cuboidf box = boxes[i];

                result[i] = new Cuboidf(
                    Math.Max(-SelectionBoxInflation, box.X1 - SelectionBoxInflation),
                    Math.Max(-SelectionBoxInflation, box.Y1 - SelectionBoxInflation),
                    Math.Max(-SelectionBoxInflation, box.Z1 - SelectionBoxInflation),
                    Math.Min(1f + SelectionBoxInflation, box.X2 + SelectionBoxInflation),
                    Math.Min(1f + SelectionBoxInflation, box.Y2 + SelectionBoxInflation),
                    Math.Min(1f + SelectionBoxInflation, box.Z2 + SelectionBoxInflation)
                );
            }

            return result;
        }

        private static Cuboidf FirstOrDefaultBox(Cuboidf[] boxes)
        {
            if (boxes != null && boxes.Length > 0)
            {
                return boxes[0];
            }

            return new Cuboidf(0.45f, 0f, 0.45f, 0.55f, 0.1f, 0.55f);
        }
    }
}
