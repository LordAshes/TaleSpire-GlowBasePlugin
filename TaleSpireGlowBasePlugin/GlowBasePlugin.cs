using UnityEngine;
using BepInEx;
using Bounce.Unmanaged;
using System.Collections.Generic;
using System.Linq;

namespace LordAshes
{
    [BepInPlugin(Guid, "Glow Base Plug-In", Version)]
    [BepInDependency(RadialUI.RadialUIPlugin.Guid)]
    [BepInDependency(StatMessaging.Guid)]
    public class GlowBasePlugin : BaseUnityPlugin
    {
        // Plugin info
        private const string Guid = "org.lordashes.plugins.glowbase";
        private const string Version = "1.0.0.0";

        // Content directory
        private string dir = UnityEngine.Application.dataPath.Substring(0, UnityEngine.Application.dataPath.LastIndexOf("/")) + "/TaleSpire_CustomData/";

        // Holds the creature of the radial menu
        private CreatureGuid radialCreature = CreatureGuid.Empty;

        // Holds color sequences
        private Dictionary<CreatureGuid, Color[]> sequenceOfColors = new Dictionary<CreatureGuid, Color[]>();
        private Dictionary<CreatureGuid, int> sequenceOfColorsPointer = new Dictionary<CreatureGuid, int>();
        private Dictionary<CreatureGuid, Texture> originalTexture = new Dictionary<CreatureGuid, Texture>();

        // Seqeucen Steps
        private const int sequenceSteps = 50;

        // Last selected mini
        private CreatureGuid lastSelected = CreatureGuid.Empty;

        /// <summary>
        /// Function for initializing plugin
        /// This function is called once by TaleSpire
        /// </summary>
        void Awake()
        {
            UnityEngine.Debug.Log("Lord Ashes Glow Base Plugin Active.");

            Texture2D tex = new Texture2D(32, 32);
            tex.LoadImage(System.IO.File.ReadAllBytes(dir + "Images/Icons/BasePaint.Png"));
            Sprite icon = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f));

            RadialUI.RadialUIPlugin.AddOnCharacter(Guid, new MapMenu.ItemArgs
            {
                Action = (mmi,obj)=>
                {
                    // When paint base is selected post it to StatMessaging (distribute to all clients)
                    SystemMessage.AskForTextInput("Paint Base", "Color(s): ", "Paint", (color) => 
                    {
                        StatMessaging.SetInfo(radialCreature, GlowBasePlugin.Guid, color);
                    }, null, "Normal", () => 
                    {
                        StatMessaging.SetInfo(radialCreature, GlowBasePlugin.Guid, "");
                    },
                    "");
                },
                Icon = icon,
                Title = "Paint Base",
                CloseMenuOnActivate = true
            }, Reporter);        

            StatMessaging.Subscribe(GlowBasePlugin.Guid, (changes)=>
            {
                // When knockover message is received
                foreach(StatMessaging.Change change in changes)
                {
                    // Process knockover
                    CreatureBoardAsset asset;
                    CreaturePresenter.TryGetAsset(change.cid, out asset);
                    if(asset!=null)
                    {
                        if(change.value=="")
                        {
                            Debug.Log("Turning Glow Color Off");
                            try
                            {
                                asset.BaseLoader.LoadedAsset.GetComponent<MeshRenderer>().material.mainTexture = originalTexture[asset.Creature.CreatureId];
                                originalTexture.Remove(asset.Creature.CreatureId);
                                sequenceOfColorsPointer.Remove(asset.Creature.CreatureId);
                                sequenceOfColors.Remove(asset.Creature.CreatureId);
                            }
                            catch (System.Exception) {; }
                        }
                        else
                        {
                            if (!originalTexture.ContainsKey(asset.Creature.CreatureId)) { originalTexture.Add(asset.Creature.CreatureId, asset.BaseLoader.LoadedAsset.GetComponent<MeshRenderer>().material.mainTexture); }
                            if (sequenceOfColors.ContainsKey(asset.Creature.CreatureId)) { sequenceOfColors.Remove(asset.Creature.CreatureId); }
                            if (sequenceOfColorsPointer.ContainsKey(asset.Creature.CreatureId)) { sequenceOfColorsPointer.Remove(asset.Creature.CreatureId); }
                            string[] sequence = change.value.Split(',');
                            if(sequence.Length==1)
                            {
                                // Single Color
                                Debug.Log("Setting Glow Color To One Color");
                                System.Drawing.Color target = System.Drawing.Color.FromName(sequence[0]);
                                sequenceOfColors.Add(asset.Creature.CreatureId, new Color[] { new Color((target.R / 255f), (target.G / 255f), (target.B / 255f), (target.A / 255f)) });
                            }
                            else
                            {
                                // Multiple Colors
                                Debug.Log("Setting Glow Color To Multiple Colors");
                                List<Color> colorSequence = new List<Color>();
                                for (int s = 0; s < sequence.Length; s++)
                                {
                                    System.Drawing.Color src;
                                    System.Drawing.Color dst;
                                    src = System.Drawing.Color.FromName(sequence[s]);
                                    if (s < (sequence.Length - 1)) { dst = System.Drawing.Color.FromName(sequence[s+1]); } else { dst = System.Drawing.Color.FromName(sequence[0]); }
                                    Debug.Log("Adding 5x RGBA(" + (src.R / 255f) + "," + (src.G / 255f) + "," + (src.B / 255f) + ":" + (src.A / 255f) + ")");
                                    for (int n = 0; n < sequenceSteps; n++)
                                    {
                                        colorSequence.Add(new Color((src.R / 255f), (src.G / 255f), (src.B / 255f), (src.A / 255f)));
                                    }
                                    float dR = (dst.R - src.R) / sequenceSteps;
                                    float dG = (dst.G - src.G) / sequenceSteps;
                                    float dB = (dst.B - src.B) / sequenceSteps;
                                    float dA = (dst.A - src.A) / sequenceSteps;
                                    for(int d=1; d<= sequenceSteps; d++)
                                    {
                                        Debug.Log("Adding RGBA(" + ((src.R + d * dR) / 255f) + "," + ((src.G + d * dG) / 255f) + "," + ((src.B + d * dB) / 255f) + ":" + ((src.A + d * dA) / 255f) + ")");
                                        colorSequence.Add(new Color(((src.R+d*dR) / 255f), ((src.G+d*dG) / 255f), ((src.B+d*dB) / 255f), ((src.A+d*dA) / 255f)));
                                    }
                                }
                                sequenceOfColors.Add(asset.Creature.CreatureId, colorSequence.ToArray());
                            }
                            sequenceOfColorsPointer.Add(asset.Creature.CreatureId, 0);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// Method to track which asset has the radial menu open
        /// </summary>
        /// <param name="selected"></param>
        /// <param name="radialMenu"></param>
        /// <returns></returns>
        private bool Reporter(NGuid selected, NGuid radialMenu)
        {
            radialCreature = new CreatureGuid(radialMenu);
            return true;
        }

        /// <summary>
        /// Function for determining if view mode has been toggled and, if so, activating or deactivating Character View mode.
        /// This function is called periodically by TaleSpire.
        /// </summary>
        void Update()
        {
            CreatureBoardAsset asset;
            if (LocalClient.SelectedCreatureId!=lastSelected)
            {
                // When base is not selected, restore original texture 
                CreaturePresenter.TryGetAsset(lastSelected, out asset);
                if (asset != null)
                {
                    if (originalTexture.ContainsKey(asset.Creature.CreatureId))
                    {
                        asset.BaseLoader.LoadedAsset.GetComponent<MeshRenderer>().material.mainTexture = originalTexture[asset.Creature.CreatureId];
                    }
                }
            }
            CreaturePresenter.TryGetAsset(LocalClient.SelectedCreatureId, out asset);
            if(asset!=null)
            {
                if(sequenceOfColorsPointer.ContainsKey(asset.Creature.CreatureId))
                {
                    // Seqeunce colors for selected base
                    int ptr = sequenceOfColorsPointer[asset.Creature.CreatureId];
                    ptr++;
                    if (ptr>= sequenceOfColors[asset.Creature.CreatureId].Length) { ptr = 0; }
                    sequenceOfColorsPointer[asset.Creature.CreatureId] = ptr;
                    Color[] pixels = Enumerable.Repeat(sequenceOfColors[asset.Creature.CreatureId][ptr], 16*16).ToArray();
                    Texture2D tex = new Texture2D(16, 16);
                    tex.SetPixels(pixels);
                    tex.Apply();
                    asset.BaseLoader.LoadedAsset.GetComponent<MeshRenderer>().material.mainTexture = tex;
                }
                lastSelected = LocalClient.SelectedCreatureId;
            }
        }
    }
}
