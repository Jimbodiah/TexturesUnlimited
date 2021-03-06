﻿using System;
using System.Collections.Generic;
using UnityEngine;

namespace KSPShaderTools
{
    public class CraftRecolorGUI : MonoBehaviour
    {
        private static int graphWidth = 400;
        private static int graphHeight = 540;
        private static int id;
        private static Rect windowRect = new Rect(Screen.width - 500, 40, graphWidth, graphHeight);
        private static Vector2 scrollPos;
        private static Vector2 presetColorScrollPos;
        private static GUIStyle nonWrappingLabelStyle = null;

        private List<ModuleRecolorData> moduleRecolorData = new List<ModuleRecolorData>();
        
        internal Action guiCloseAction;

        private SectionRecolorData sectionData;
        private int moduleIndex = -1;
        private int sectionIndex = -1;
        private int colorIndex = -1;
        private string rStr, gStr, bStr, aStr, mStr;//string caches of color values//TODO -- set initial state when a section color is selected
        private static RecoloringData editingColor;
        private static RecoloringData[] storedPattern;
        private static RecoloringData storedColor;

        public static Part openPart;

        public void Awake()
        {
            id = GetInstanceID();
        }

        internal void openGUIPart(Part part)
        {
            if (part != openPart)
            {
                moduleIndex = -1;
                sectionIndex = -1;
                colorIndex = -1;
            }
            if (moduleIndex < 0) { moduleIndex = 0; }
            if (sectionIndex < 0) { sectionIndex = 0; }
            if (colorIndex < 0) { colorIndex = 0; }
            ControlTypes controls = ControlTypes.ALLBUTCAMERAS;
            controls = controls & ~ControlTypes.TWEAKABLES;
            InputLockManager.SetControlLock(controls, "SSTURecolorGUILock");
            setupForPart(part);
            setupSectionData(moduleRecolorData[moduleIndex].sectionData[sectionIndex], colorIndex);
            openPart = part;
        }

        /// <summary>
        /// To be called from the external 'GuiCloseAction' delegate.
        /// </summary>
        internal void closeGui()
        {
            closeSectionGUI();
            moduleRecolorData.Clear();
            sectionData = null;
            openPart = null;
            InputLockManager.RemoveControlLock("SSTURecolorGUILock");
            colorIndex = -1;
            moduleIndex = -1;
            sectionIndex = -1;
        }

        internal void refreshGui(Part part)
        {
            //MonoBehaviour.print("Refreshing Recoloring GUI: " + part + " :: " + openPart);
            if (part != openPart) { return; }

            moduleRecolorData.Clear();
            setupForPart(part);

            int len = moduleRecolorData.Count;
            if (moduleIndex >= len) { moduleIndex = 0; sectionIndex = 0; }
            len = moduleRecolorData[moduleIndex].sectionData.Length;
            if (sectionIndex >= len) { sectionIndex = 0; }

            setupSectionData(moduleRecolorData[moduleIndex].sectionData[sectionIndex], colorIndex);
        }

        private void setupForPart(Part part)
        {
            List<IRecolorable> mods = part.FindModulesImplementing<IRecolorable>();
            foreach (IRecolorable mod in mods)
            {
                ModuleRecolorData data = new ModuleRecolorData((PartModule)mod, mod);
                moduleRecolorData.Add(data);
            }
        }

        public void OnGUI()
        {
            //apparently trying to initialize this during OnAwake/etc fails, as unity is dumb and requires that it be done during an OnGUI call
            //serious -- you cant even access the GUI.skin except in OnGUi...
            if (nonWrappingLabelStyle == null)
            {
                GUIStyle style = new GUIStyle(GUI.skin.label);
                style.wordWrap = false;
                nonWrappingLabelStyle = style;
            }
            windowRect = GUI.Window(id, windowRect, drawWindow, "Part Recoloring");
        }

        private void drawWindow(int id)
        {
            GUILayout.BeginVertical();
            drawSectionSelectionArea();
            drawSectionRecoloringArea();
            drawPresetColorArea();
            if (GUILayout.Button("Close"))
            {
                guiCloseAction();//call the method in SSTULauncher to close this GUI
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        private void setupSectionData(SectionRecolorData section, int colorIndex)
        {
            this.sectionData = section;
            this.colorIndex = colorIndex;
            if (section.colors == null) { return; }
            editingColor = sectionData.colors[colorIndex];
            rStr = (editingColor.color.r * 255f).ToString("F0");
            gStr = (editingColor.color.g * 255f).ToString("F0");
            bStr = (editingColor.color.b * 255f).ToString("F0");
            aStr = (editingColor.specular * 255f).ToString("F0");
            mStr = (editingColor.metallic * 255f).ToString("F0");
        }

        private void closeSectionGUI()
        {
            sectionData = null;
            editingColor = new RecoloringData(Color.white, 0, 0);
            rStr = gStr = bStr = aStr = "255";
            colorIndex = 0;
        }

        private void drawSectionSelectionArea()
        {
            GUILayout.BeginHorizontal();
            Color old = GUI.color;
            float buttonWidth = 70;
            float scrollWidth = 40;
            float sectionTitleWidth = graphWidth - scrollWidth - buttonWidth * 3 - scrollWidth;
            GUILayout.Label("Section", GUILayout.Width(sectionTitleWidth));
            GUI.color = colorIndex == 0 ? Color.red : old;
            GUILayout.Label("Main", GUILayout.Width(buttonWidth));
            GUI.color = colorIndex == 1 ? Color.red : old;
            GUILayout.Label("Second", GUILayout.Width(buttonWidth));
            GUI.color = colorIndex == 2 ? Color.red : old;
            GUILayout.Label("Detail", GUILayout.Width(buttonWidth));
            GUI.color = old;
            GUILayout.EndHorizontal();
            Color guiColor = old;
            scrollPos = GUILayout.BeginScrollView(scrollPos, false, true, GUILayout.Height(100));
            int len = moduleRecolorData.Count;
            for (int i = 0; i < len; i++)
            {
                int len2 = moduleRecolorData[i].sectionData.Length;
                for (int k = 0; k < len2; k++)
                {
                    if (!moduleRecolorData[i].sectionData[k].recoloringSupported())
                    {
                        continue;
                    }
                    GUILayout.BeginHorizontal();
                    if ( k == sectionIndex && i == moduleIndex )
                    {
                        GUI.color = Color.red;
                    }
                    GUILayout.Label(moduleRecolorData[i].sectionData[k].sectionName, GUILayout.Width(sectionTitleWidth));
                    for (int m = 0; m < 3; m++)
                    {
                        int mask = 1 << m;
                        if (moduleRecolorData[i].sectionData[k].channelSupported(mask))
                        {
                            guiColor = moduleRecolorData[i].sectionData[k].colors[m].color;
                            guiColor.a = 1;
                            GUI.color = guiColor;
                            if (GUILayout.Button("Recolor", GUILayout.Width(70)))
                            {
                                moduleIndex = i;
                                sectionIndex = k;
                                colorIndex = m;
                                setupSectionData(moduleRecolorData[i].sectionData[k], m);
                            }
                        }
                        else
                        {
                            GUILayout.Label("", GUILayout.Width(70));
                        }
                    }
                    GUI.color = old;
                    GUILayout.EndHorizontal();
                }
            }
            GUI.color = old;
            GUILayout.EndScrollView();
        }

        private void drawSectionRecoloringArea()
        {            
            if (sectionData == null)
            {
                return;
            }
            bool updated = false;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Editing: ", GUILayout.Width(60));
            GUILayout.Label(sectionData.sectionName);
            GUILayout.Label(getSectionLabel(colorIndex) + " Color");
            GUILayout.FlexibleSpace();//to force everything to the left instead of randomly spaced out, while still allowing dynamic length adjustments
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Red", ref editingColor.color.r, ref rStr, sectionData.colorSupported())) { updated = true; }
            if (GUILayout.Button("Load Pattern", GUILayout.Width(120)))
            {
                sectionData.colors[0] = storedPattern[0];
                sectionData.colors[1] = storedPattern[1];
                sectionData.colors[2] = storedPattern[2];
                editingColor = sectionData.colors[colorIndex];
                updated = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Green", ref editingColor.color.g, ref gStr, sectionData.colorSupported())) { updated = true; }
            if (GUILayout.Button("Store Pattern", GUILayout.Width(120)))
            {
                storedPattern = new RecoloringData[3];
                storedPattern[0] = sectionData.colors[0];
                storedPattern[1] = sectionData.colors[1];
                storedPattern[2] = sectionData.colors[2];
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Blue", ref editingColor.color.b, ref bStr, sectionData.colorSupported())) { updated = true; }
            if (GUILayout.Button("Load Color", GUILayout.Width(120)))
            {
                editingColor = storedColor;
                updated = true;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (drawColorInputLine("Specular", ref editingColor.specular, ref aStr, sectionData.specularSupported())) { updated = true; }
            if (GUILayout.Button("Store Color", GUILayout.Width(120)))
            {
                storedColor = editingColor;
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (sectionData.metallicSupported())
            {
                if (drawColorInputLine("Metallic", ref editingColor.metallic, ref mStr, true)) { updated = true; }
            }
            else if (sectionData.hardnessSupported())
            {
                if (drawColorInputLine("Hardness", ref editingColor.metallic, ref mStr, true)) { updated = true; }
            }
            else
            {
                if (drawColorInputLine("Metallic", ref editingColor.metallic, ref mStr, false)) { updated = true; }
            }
            GUILayout.EndHorizontal();

            if (updated)
            {
                sectionData.colors[colorIndex] = editingColor;
                sectionData.updateColors();
            }
        }

        private void drawPresetColorArea()
        {
            if (sectionData == null)
            {
                return;
            }
            GUILayout.Label("Select a preset color: ");
            presetColorScrollPos = GUILayout.BeginScrollView(presetColorScrollPos, false, true);
            bool update = false;
            Color old = GUI.color;
            Color guiColor = old;
            List<RecoloringDataPreset> presetColors = PresetColor.getColorList();
            int len = presetColors.Count;
            GUILayout.BeginHorizontal();
            for (int i = 0; i < len; i++)
            {
                if (i > 0 && i % 2 == 0)
                {
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                }
                GUILayout.Label(presetColors[i].title, nonWrappingLabelStyle, GUILayout.Width(115));
                guiColor = presetColors[i].color;
                guiColor.a = 1f;
                GUI.color = guiColor;
                if (GUILayout.Button("Select", GUILayout.Width(55)))
                {
                    editingColor = presetColors[i].getRecoloringData();
                    rStr = (editingColor.color.r * 255f).ToString("F0");
                    gStr = (editingColor.color.g * 255f).ToString("F0");
                    bStr = (editingColor.color.b * 255f).ToString("F0");
                    aStr = (editingColor.specular * 255f).ToString("F0");
                    mStr = (editingColor.metallic * 255f).ToString("F0");
                    update = true;
                }
                GUI.color = old;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
            GUI.color = old;
            if (sectionData.colors != null)
            {
                sectionData.colors[colorIndex] = editingColor;
                if (update)
                {
                    sectionData.updateColors();
                }
            }
        }

        private bool drawColorInputLine(string label, ref float val, ref string sVal, bool enabled)
        {
            if (!enabled)
            {
                GUILayout.Label("", GUILayout.Width(60 + 120 + 60));
                return false;
            }
            //TODO -- text input validation for numbers only -- http://answers.unity3d.com/questions/18736/restrict-characters-in-guitextfield.html
            // also -- https://forum.unity3d.com/threads/text-field-for-numbers-only.106418/
            GUILayout.Label(label, GUILayout.Width(60));
            bool updated = false;
            float result = val;
            result = GUILayout.HorizontalSlider(val, 0, 1, GUILayout.Width(120));
            if (result != val)
            {
                val = result;
                sVal = (val * 255f).ToString("F0");
                updated = true;
            }
            string textOutput = GUILayout.TextField(sVal, 3, GUILayout.Width(60));
            if (sVal != textOutput)
            {
                sVal = textOutput;
                int iVal;
                if (int.TryParse(textOutput, out iVal))
                {
                    val = iVal / 255f;
                    updated = true;
                }
            }
            return updated;
        }

        private string getSectionLabel(int index)
        {
            switch (index)
            {
                case 0:
                    return "Main";
                case 1:
                    return "Secondary";
                case 2:
                    return "Detail";
                default:
                    return "Unknown";
            }
        }

    }

    public class ModuleRecolorData
    {
        public PartModule module;//must implement IRecolorable
        public IRecolorable iModule;//interface version of module
        public SectionRecolorData[] sectionData;

        public ModuleRecolorData(PartModule module, IRecolorable iModule)
        {
            this.module = module;
            this.iModule = iModule;
            string[] names = iModule.getSectionNames();
            int len = names.Length;
            sectionData = new SectionRecolorData[len];
            for (int i = 0; i < len; i++)
            {
                sectionData[i] = new SectionRecolorData(iModule, names[i], iModule.getSectionColors(names[i]), iModule.getSectionTexture(names[i]));
            }
        }
    }

    public class SectionRecolorData
    {
        public readonly IRecolorable owner;
        public readonly string sectionName;
        public RecoloringData[] colors;
        private TextureSet sectionTexture;

        public SectionRecolorData(IRecolorable owner, string name, RecoloringData[] colors, TextureSet set)
        {
            this.owner = owner;
            this.sectionName = name;
            this.colors = colors;
            this.sectionTexture = set;
            if (colors == null)
            {
                //owners may return null for set and/or colors if recoloring is unsupported
                set = sectionTexture = null;
            }
            MonoBehaviour.print("Created section recolor data with texture set: " + set+" for section: "+name);
            if (set != null)
            {
                MonoBehaviour.print("Set name: " + set.name + " :: " + set.title + " recolorable: " + set.supportsRecoloring);
            }
            else
            {
                MonoBehaviour.print("Set was null");
            }
        }

        public void updateColors()
        {
            owner.setSectionColors(sectionName, colors);
        }

        public bool recoloringSupported()
        {
            if (sectionTexture == null) { return false; }
            return sectionTexture.supportsRecoloring;
        }

        public bool colorSupported()
        {
            if (sectionTexture == null) { return false; }
            return (sectionTexture.featureMask & 1) != 0;
        }

        public bool channelSupported(int mask)
        {
            if (sectionTexture == null) { return false; }
            return (sectionTexture.recolorableChannelMask & mask) != 0;
        }

        public bool specularSupported()
        {
            if (sectionTexture == null) { return false; }
            return (sectionTexture.featureMask & 2) != 0;
        }

        public bool metallicSupported()
        {
            if (sectionTexture == null) { return false; }
            return (sectionTexture.featureMask & 4) != 0;
        }

        public bool hardnessSupported()
        {
            if (sectionTexture == null) { return false; }
            return (sectionTexture.featureMask & 8) != 0;
        }

    }

}
