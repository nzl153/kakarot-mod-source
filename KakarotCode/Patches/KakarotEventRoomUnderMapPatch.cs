#nullable disable
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KakarotMod.KakarotCode.Patches;

/// <summary>地图打开时隐藏当前事件房，关闭地图后仅在仍处于事件房时恢复显示。</summary>
public static class KakarotEventRoomUnderMapPatch
{
    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
    public static class HideEventRoomWhenMapOpens
    {
        public static void Postfix()
        {
            var er = NRun.Instance?.EventRoom;
            if (er != null)
            {
                er.Visible = false;
            }
        }
    }

    [HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
    public static class ShowEventRoomWhenMapCloses
    {
        public static void Postfix()
        {
            var er = NRun.Instance?.EventRoom;
            if (er != null)
            {
                er.Visible = true;
            }
        }
    }
}
