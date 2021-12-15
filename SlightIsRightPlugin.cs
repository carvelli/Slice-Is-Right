using Dalamud.Game.ClientState;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Gui;
using Dalamud.Interface;
using Dalamud.IoC;
using Dalamud.Plugin;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.InteropServices;

namespace SliceIsRight
{
    public sealed class SliceIsRightPlugin : IDalamudPlugin
    {
        private const float HALF_PI = (float)Math.PI / 2f;
        private uint COLOUR_BLUE = ImGui.GetColorU32(ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 0.0f, 1f, 0.15f)));
        private uint COLOUR_GREEN = ImGui.GetColorU32(ImGui.ColorConvertFloat4ToU32(new Vector4(0.0f, 1f, 0.0f, 0.15f)));
        private uint COLOUR_RED = ImGui.GetColorU32(ImGui.ColorConvertFloat4ToU32(new Vector4(1, 0, 0, 0.4f)));

        public string Name => "Slice is Right";

        [PluginService]
        [RequiredVersion("1.0")]
        private DalamudPluginInterface PluginInterface { get; set; }

        [PluginService]
        private ObjectTable ObjectTable { get; set; }

        [PluginService]
        private GameGui GameGui { get; set; }

        [PluginService]
        private ClientState ClientState { get; set; }
        
        private const ushort GoldSaucerTerritoryId = 144;
        private bool IsInGoldSaucer { get; set; }

        private readonly IDictionary<uint, DateTime> objectsAndSpawnTime = new Dictionary<uint, DateTime>();
        private readonly ISet<uint> objectsToMatch = new HashSet<uint>();

        private const float MaxDistance = 30f;

#pragma warning disable CS8618
        public SliceIsRightPlugin()
        {
            PluginInterface!.UiBuilder.Draw += DrawUI;
            ClientState!.TerritoryChanged += TerritoryChanged;
            IsInGoldSaucer = ClientState.TerritoryType == GoldSaucerTerritoryId;
        }
#pragma warning restore CS8618

        private void TerritoryChanged(object? sender, ushort e)
        {
            IsInGoldSaucer = e == GoldSaucerTerritoryId;
        }

        public void Dispose()
        {
            PluginInterface.UiBuilder.Draw -= DrawUI;
            ClientState.TerritoryChanged -= TerritoryChanged;
        }

        private void DrawUI()
        {
            if (!ClientState.IsLoggedIn || !IsInGoldSaucer || ObjectTable == null)
                return;

            for (int index = 0; index < ObjectTable.Length; ++ index)
            {
                GameObject? obj = ObjectTable[index];
                if (obj == null || DistanceToPlayer(obj.Position) > MaxDistance)
                    continue;

                int model = Marshal.ReadInt32(obj.Address + 128);
                if (obj.ObjectKind == ObjectKind.EventObj && (model >= 2010777 && model <= 2010779))
                {
                    RenderObject(index, obj, model);
                }
                else if (ClientState.LocalPlayer?.ObjectId == obj.ObjectId)
                {
                    // local player
                    //RenderObject(index, obj, 2010779, 0.1f); // circle
                    //RenderObject(index, obj, 2010778, 30f); // falls to both sides
                    //RenderObject(index, obj, 2010777, 30f); // falls to one side
                }
            }

            foreach (uint objectId in objectsToMatch)
                objectsAndSpawnTime.Remove(objectId);
            objectsToMatch.Clear();
        }

        private void RenderObject(int index, GameObject obj, int model, float? radius = null)
        {
            objectsToMatch.Remove(obj.ObjectId);

            if (objectsAndSpawnTime.TryGetValue(obj.ObjectId, out DateTime spawnTime))
            {
                if (spawnTime.AddSeconds(5) > DateTime.Now)
                    return;
            }
            else
            {
                objectsAndSpawnTime.Add(obj.ObjectId, DateTime.Now);
                return;
            }


            switch (model)
            {
                case 2010777:
                    DrawRectWorld(obj, obj.Rotation + HALF_PI, radius ?? 25f, 5f, COLOUR_BLUE);
                    break;

                case 2010778:
                    DrawRectWorld(obj, obj.Rotation + HALF_PI, radius ?? 25f, 5f, COLOUR_GREEN);
                    DrawRectWorld(obj, obj.Rotation - HALF_PI, radius ?? 25f, 5f, COLOUR_GREEN);
                    break;

                case 2010779:
                //default:
                    DrawFilledCircleWorld(obj, radius ?? 11f, COLOUR_RED);
                    break;
            }
        }

        private void BeginRender(string name)
        {
            ImGui.PushID("sliceWindowI" + name);

            ImGui.PushStyleVar((ImGuiStyleVar)1, new Vector2(0.0f, 0.0f));
            ImGuiHelpers.SetNextWindowPosRelativeMainViewport(new Vector2(0.0f, 0.0f), ImGuiCond.None, new Vector2());
            ImGui.Begin("sliceWindow" + name, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoInputs);
            ImGui.SetWindowSize(ImGui.GetIO().DisplaySize);
        }

        private void EndRender()
        {
            ImGui.End();
            ImGui.PopStyleVar();

            ImGui.PopID();
        }

        private void DrawFilledCircleWorld(GameObject obj, float radius, uint colour)
        {
            BeginRender(obj.Address.ToString());

            var center = obj.Position;
            int segmentCount = 100;
            bool onScreen = false;
            for (int index = 0; index <= 2 * segmentCount; ++index)
            {
                onScreen |= GameGui.WorldToScreen(new Vector3(center.X + radius * (float)Math.Sin(Math.PI / segmentCount * index), center.Y, center.Z + radius * (float)Math.Cos(Math.PI / segmentCount * index)), out Vector2 vector2);
                ImGui.GetWindowDrawList().PathLineTo(vector2);
            }
            
            if (onScreen)
                ImGui.GetWindowDrawList().PathFillConvex(colour);
            else
                ImGui.GetWindowDrawList().PathClear();

            EndRender();
        }

        private void DrawRectWorld(GameObject obj, float rotation, float length, float width, uint colour)
        {
            BeginRender(obj.Address.ToString() + obj.Rotation.ToString());

            var center = obj.Position;
            Vector2 displaySize = ImGui.GetIO().DisplaySize;
            Vector3 near1 = new Vector3(center.X + width / 2 * (float)Math.Sin(HALF_PI + rotation), center.Y, center.Z + width / 2 * (float)Math.Cos(HALF_PI + rotation));
            Vector3 near2 = new Vector3(center.X + width / 2 * (float)Math.Sin(rotation - HALF_PI), center.Y, center.Z + width / 2 * (float)Math.Cos(rotation - HALF_PI));
            Vector3 nearCenter = new Vector3(center.X, center.Y, center.Z);
            int rectangleCount = 20;
            float lengthSlice = length / rectangleCount;

            var drawList = ImGui.GetWindowDrawList();
            for (int index = 1; index <= rectangleCount; ++index)
            {
                Vector3 far1 = new Vector3(near1.X + lengthSlice * (float)Math.Sin(rotation), near1.Y, near1.Z + lengthSlice * (float)Math.Cos(rotation));
                Vector3 far2 = new Vector3(near2.X + lengthSlice * (float)Math.Sin(rotation), near2.Y, near2.Z + lengthSlice * (float)Math.Cos(rotation));
                Vector3 farCenter = new Vector3(nearCenter.X + lengthSlice * (float)Math.Sin(rotation), nearCenter.Y, nearCenter.Z + lengthSlice * (float)Math.Cos(rotation));

                bool onScreen = false;
                foreach (Vector3 v in new[]{ far2, farCenter, far1, near1, nearCenter, near2}) 
                {
                    onScreen |= GameGui.WorldToScreen(v, out Vector2 nextVertex);
                    if ((nextVertex.X > 0 & nextVertex.X < displaySize.X) || (nextVertex.Y > 0 & nextVertex.Y < displaySize.Y))
                    {
                        drawList.PathLineTo(nextVertex);
                    }
                }

                if (onScreen)
                    drawList.PathFillConvex(colour);
                else
                    drawList.PathClear();

                near1 = far1;
                near2 = far2;
                nearCenter = farCenter;
            }

            EndRender();
        }

        private float DistanceToPlayer(Vector3 center)
        {
            return Vector3.Distance(ClientState.LocalPlayer?.Position ?? Vector3.Zero, center);
        }
    }
}
