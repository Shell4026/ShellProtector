#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using System.Security.Cryptography;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Avatars.Components;

#if POIYOMI
using Thry;
#endif

namespace Shell.Protector
{
    public class ShellProtector : MonoBehaviour
    {
        [SerializeField]
        List<Material> material_list = new List<Material>();
        [SerializeField]
        List<Texture2D> texture_list = new List<Texture2D>();

        EncryptTexture encrypt = new EncryptTexture();
        Injector injector;
        ShaderManager shader_manager = ShaderManager.GetInstance();

        public string asset_dir = "Assets/ShellProtect";
        public string pwd = "password";
        public string pwd2 = "pass";
        public int lang_idx = 0;
        public string lang = "kor";

        [SerializeField] int rounds = 32;
        [SerializeField] int filter = 1;
        [SerializeField] int algorithm = 0;
        [SerializeField] int key_size_idx = 0;
        [SerializeField] int key_size = 4;
        [SerializeField] float animation_speed = 10.0f;
        [SerializeField] bool delete_folders = true;
        public static byte[] MakeKeyBytes(string _key1, string _key2, int key2_length = 4)
        {
            SHA256 sha256 = SHA256.Create();

            byte[] key = new byte[16] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            byte[] key_bytes = Encoding.ASCII.GetBytes(_key1);

            byte[] key_bytes2 = Encoding.ASCII.GetBytes(_key2);
            byte[] hash = sha256.ComputeHash(key_bytes2);

            for (int i = 0; i < key_bytes.Length; ++i)
                key[i] = key_bytes[i];

            if (key2_length > 0)
            {
                for (int i = 0; i < key_bytes2.Length; ++i)
                    key[i + (16 - key2_length)] = key_bytes2[i];
                for (int i = 0; i < key2_length; ++i)
                    key[i + (16 - key2_length)] ^= hash[i];
            }
            return key;
        }
        public byte[] GetKeyBytes()
        {
            return MakeKeyBytes(pwd, pwd2, key_size);
        }
        public EncryptTexture GetEncryptTexture()
        {
            return encrypt;
        }
        public void Test()
        {
            Debug.Log("Test");
        }
        public void Test2()
        {
            byte[] data_byte = new byte[12] { 255, 250, 245, 240, 235, 230, 225, 220, 215, 210, 205, 200 };
            byte[] key_byte = MakeKeyBytes(pwd, pwd2, key_size);

            uint[] data = new uint[3];
            data[0] = (uint)(data_byte[0] | (data_byte[1] << 8) | (data_byte[2] << 16) | (data_byte[3] << 24));
            data[1] = (uint)(data_byte[4] | (data_byte[5] << 8) | (data_byte[6] << 16) | (data_byte[7] << 24));
            data[2] = (uint)(data_byte[8] | (data_byte[9] << 8) | (data_byte[10] << 16) | (data_byte[11] << 24));

            uint[] key = new uint[4];
            key[0] = (uint)(key_byte[0] | (key_byte[1] << 8) | (key_byte[2] << 16) | (key_byte[3] << 24));
            key[1] = (uint)(key_byte[4] | (key_byte[5] << 8) | (key_byte[6] << 16) | (key_byte[7] << 24));
            key[2] = (uint)(key_byte[8] | (key_byte[9] << 8) | (key_byte[10] << 16) | (key_byte[11] << 24));
            key[3] = (uint)(key_byte[12] | (key_byte[13] << 8) | (key_byte[14] << 16) | (key_byte[15] << 24));

            Debug.Log("Key bytes: " + string.Join(", ", key_byte));
            Debug.Log(string.Format("key1:{0}, key2:{1}, key3:{2}", key[0], key[1], key[2]));
            Debug.Log("Data: " + string.Join(", ", data));

            uint[] result = XXTEA.Encrypt(data, key);
            Debug.Log("Encrypted data: " + string.Join(", ", result));

            result = XXTEA.Decrypt(result, key);
            Debug.Log("Decrypted data: " + string.Join(", ", result));
        }
        public static void SetRWEnableTexture(Texture2D texture)
        {
            if (texture.isReadable)
                return;
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            meta = Regex.Replace(meta, "isReadable: 0", "isReadable: 1");
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.Refresh();
        }
        public static void SetCrunchCompression(Texture2D texture, bool crunch)
        {
            string path = AssetDatabase.GetAssetPath(texture);
            string meta = File.ReadAllText(path + ".meta");

            int enable = crunch ? 1 : 0;
            meta = Regex.Replace(meta, "crunchedCompression: \\d", "crunchedCompression: " + enable);
            File.WriteAllText(path + ".meta", meta);

            AssetDatabase.Refresh();
        }
        public GameObject DuplicateAvatar(GameObject avatar)
        {
            GameObject cpy = Instantiate(avatar);
            cpy.name = avatar.name + "_encrypted";
            return cpy;
        }

        bool ConditionCheck(Material mat)
        {
            if (shader_manager.IsPoiyomi(mat.shader))
            {
                if (!shader_manager.IsLockPoiyomi(mat.shader))
                {
#if POIYOMI
                    ShaderOptimizer.SetLockedForAllMaterials(new[] { mat }, 1, true);
#endif
                }
            }
            if (mat.mainTexture == null)
            {
                Debug.LogErrorFormat("{0} : The mainTexture is empty. it will be skip.", mat.name);
                return false;
            }
            if (mat.mainTexture.width % 2 != 0 && mat.mainTexture.height % 2 != 0)
            {
                Debug.LogErrorFormat("{0} : The texture size must be a multiple of 2!", mat.mainTexture.name);
                return false;
            }
            if (injector.WasInjected(mat.shader))
            {
                Debug.LogWarning(mat.name + ": The shader is already encrypted.");
                return false;
            }
            var av3 = gameObject.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            if (av3 == null)
            {
                Debug.LogWarning(gameObject.name + ": can't find VRCAvatarDescriptor!");
                return false;
            }
            if(av3.expressionParameters == null)
            {
                Debug.LogWarning(gameObject.name + ": can't find expressionParmeters!");
                return false;
            }
            return true;
        }

        public void CreateFolders()
        {
            if (!AssetDatabase.IsValidFolder(asset_dir + '/' + gameObject.name))
                AssetDatabase.CreateFolder(asset_dir, gameObject.name);
            else
            {
                if (delete_folders)
                {
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "animations"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "mat"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "shader"));
                    AssetDatabase.DeleteAsset(Path.Combine(asset_dir, gameObject.name, "tex"));
                }
            }
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "tex")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "tex");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "mat")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "mat");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "shader")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "shader");
            if (!AssetDatabase.IsValidFolder(Path.Combine(asset_dir, gameObject.name, "animations")))
                AssetDatabase.CreateFolder(Path.Combine(asset_dir, gameObject.name), "animations");
        }

        public void Encrypt()
        {
            gameObject.SetActive(true);
            Debug.Log("Key bytes: " + string.Join(", ", GetKeyBytes()));

            GameObject avatar = DuplicateAvatar(gameObject);
            if (avatar == null)
            {
                Debug.LogError("Cannot create duplicated avatar!");
                return;
            }

            var mips = new Dictionary<int, Texture2D>();
            HashSet<GameObject> meshes = new HashSet<GameObject>();

            byte[] key_bytes = GetKeyBytes();

            CreateFolders();

            int progress = 0;
            foreach (var mat in material_list)
            {
                if (mat == null)
                {
                    ++progress;
                    Debug.LogErrorFormat("mat is null!");
                    continue;
                }
                EditorUtility.DisplayProgressBar("Encrypt...", "Encrypt Progress " + ++progress + " of " + material_list.Count, (float)progress / (float)material_list.Count);
                injector = InjectorFactory.GetInjector(mat.shader);
                if (injector == null)
                {
                    Debug.LogWarning(mat.shader + " is a unsupported shader! supported type:lilToon, poiyomi");
                    continue;
                }
                if (!ConditionCheck(mat))
                    continue;

                Debug.LogFormat("{0} : start encrypt...", mat.name);
                injector.Init(gameObject, key_bytes, key_size, filter, asset_dir);

                #region Generate mip_tex
                int size = Math.Max(mat.mainTexture.width, mat.mainTexture.height);
                if (!mips.ContainsKey(size))
                {
                    var mip = encrypt.GenerateRefMipmap(size, size);
                    if (mip == null)
                        Debug.LogErrorFormat("{0} : Can't generate mip tex{1}.", mat.name, size);
                    else
                    {
                        mips.Add(size, mip);
                        AssetDatabase.CreateAsset(mip, Path.Combine(asset_dir, gameObject.name, "tex", "mip_" + size + ".asset"));
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh();
                    }
                }
                #endregion
                Texture2D main_texture = (Texture2D)mat.mainTexture;
                SetRWEnableTexture(main_texture);
                SetCrunchCompression(main_texture, false);

                Texture2D lim_texture = null;
                Texture2D lim_texture2 = null;
                Texture2D outline_texture = null;
                bool has_lim_texture = false;
                bool has_lim_texture2 = false;
                bool has_outline_texture = false;
                #region Get lim, outline tex
                if (shader_manager.IsPoiyomi(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach(var t in tex_properties)
                    {
                        if(t == "_RimTex")
                            lim_texture = (Texture2D)mat.GetTexture(t);
                        else if(t == "_Rim2Tex")
                            lim_texture2 = (Texture2D)mat.GetTexture(t);
                        else if(t == "_OutlineTexture")
                            outline_texture = (Texture2D)mat.GetTexture(t);
                    }
                }
                else if (shader_manager.IslilToon(mat.shader))
                {
                    var tex_properties = mat.GetTexturePropertyNames();
                    foreach (var t in tex_properties)
                    {
                        if(t == "_RimColorTex")
                            lim_texture = (Texture2D)mat.GetTexture(t);
                        if (t == "_OutlineTex")
                            outline_texture = (Texture2D)mat.GetTexture(t);
                    }
                }
                if (lim_texture != null)
                {
                    if (main_texture.GetInstanceID() == lim_texture.GetInstanceID())
                        has_lim_texture = true;
                }
                if (lim_texture2 != null)
                {
                    if (main_texture.GetInstanceID() == lim_texture2.GetInstanceID())
                        has_lim_texture2 = true;
                }
                if (outline_texture != null)
                {
                    if (main_texture.GetInstanceID() == outline_texture.GetInstanceID())
                        has_outline_texture = true;
                }
                #endregion
                string encrypt_tex_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + "_encrypt.asset");
                string encrypt_tex2_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + "_encrypt2.asset");
                string encrypted_mat_path = Path.Combine(asset_dir, gameObject.name, "mat", mat.name + "_encrypted.mat");
                string encrypted_shader_path = Path.Combine(asset_dir, gameObject.name, "shader", mat.name);
                Texture2D[] encrypted_tex = new Texture2D[2] { null, null };

                #region Textures Duplicate Check
                bool has_exist_encrypt_tex = false;
                bool has_exist_encrypt_tex2 = false;
                foreach (var mat_tmp in material_list)
                {
                    if (mat_tmp == mat)
                        continue;

                    Texture2D main_tex = mat_tmp.mainTexture as Texture2D;
                    if (main_tex == null)
                        continue;

                    if(main_texture.GetInstanceID() == main_tex.GetInstanceID())
                    {
                        encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                        encrypted_tex[1] = AssetDatabase.LoadAssetAtPath(encrypt_tex2_path, typeof(Texture2D)) as Texture2D;
                        if (encrypted_tex[0] != null)
                            has_exist_encrypt_tex = true;
                        if (encrypted_tex[1] != null)
                            has_exist_encrypt_tex2 = true;
                        break;
                    }
                    else
                    {
                        if(main_texture.name == main_tex.name)
                        {
                            encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                            int idx = 0;
                            while (encrypted_tex[0] != null)
                            {
                                encrypt_tex_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + idx + "_encrypt.asset");
                                encrypt_tex2_path = Path.Combine(asset_dir, gameObject.name, "tex", main_texture.name + idx + "_encrypt2.asset");

                                encrypted_tex[0] = AssetDatabase.LoadAssetAtPath(encrypt_tex_path, typeof(Texture2D)) as Texture2D;
                                ++idx;
                            }
                        }
                    }
                }
                #endregion

                #region Materials Duplicate Check
                for (int i = 0; i < material_list.Count; ++i)
                {
                    if (material_list[i] == null)
                        continue;
                    if (mat.GetInstanceID() == material_list[i].GetInstanceID())
                        continue;
                    else
                    {
                        if(mat.name == material_list[i].name)
                        {
                            Material m = AssetDatabase.LoadAssetAtPath(encrypted_mat_path, typeof(Material)) as Material;
                            int idx = 0;
                            while(m != null)
                            {
                                encrypted_mat_path = Path.Combine(asset_dir, gameObject.name, "mat", mat.name + idx + "_encrypted.mat");
                                encrypted_shader_path = Path.Combine(asset_dir, gameObject.name, "shader", mat.name + idx);
                                m = AssetDatabase.LoadAssetAtPath(encrypted_mat_path, typeof(Material)) as Material;
                                ++idx;
                            }
                        }
                    }    
                }
                #endregion

                #region Make encrypted textures
                if (has_exist_encrypt_tex == false)
                {
                    encrypted_tex = encrypt.TextureEncryptXXTEA(main_texture, key_bytes);
                    if (encrypted_tex[0] == null)
                    {
                        Debug.LogErrorFormat("{0} : encrypt failed.", main_texture.name);
                        continue;
                    }
                    AssetDatabase.CreateAsset(encrypted_tex[0], encrypt_tex_path);
                    Debug.Log(encrypted_tex[0].name + ": " + AssetDatabase.GetAssetPath(encrypted_tex[0]));
                }
                if (has_exist_encrypt_tex2 == false)
                {
                    if (encrypted_tex[1] != null)
                        AssetDatabase.CreateAsset(encrypted_tex[1], encrypt_tex2_path);
                }
                #endregion

                Shader encrypted_shader;
                try
                {
                    encrypted_shader = injector.Inject(mat, Path.Combine(asset_dir, "Decrypt.cginc"), encrypted_shader_path, encrypted_tex[0], has_lim_texture, has_lim_texture2, has_outline_texture);
                    if (encrypted_shader == null)
                    {
                        Debug.LogWarning("Injection failed");
                        continue;
                    }
                }
                catch (UnityException e)
                {
                    Debug.LogError(e.Message);
                    continue;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                #region Material
                Material new_mat = new Material(mat.shader);
                new_mat.CopyPropertiesFromMaterial(mat);
                new_mat.shader = encrypted_shader;
                var original_tex = new_mat.mainTexture;
                new_mat.mainTexture = encrypted_tex[0];

                int max = Math.Max(encrypted_tex[0].width, encrypted_tex[0].height);
                var mip_tex = mips[max];
                if(mip_tex == null)
                    Debug.LogWarningFormat("mip_{0} is not exsist", max);

                new_mat.SetTexture("_MipTex", mip_tex);

                if (encrypted_tex[1] != null)
                    new_mat.SetTexture("_EncryptTex", encrypted_tex[1]);

                if(has_lim_texture)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_RimTex", encrypted_tex[0]);
                    else if (shader_manager.IslilToon(mat.shader))
                        new_mat.SetTexture("_RimColorTex", encrypted_tex[0]);
                }
                if(has_lim_texture2)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_Rim2Tex", encrypted_tex[0]);
                }
                if(has_outline_texture)
                {
                    if (shader_manager.IsPoiyomi(mat.shader))
                        new_mat.SetTexture("_OutlineTexture", encrypted_tex[0]);
                    else if(shader_manager.IslilToon(mat.shader))
                        new_mat.SetTexture("_OutlineTex", encrypted_tex[0]);
                }

                new_mat.renderQueue = mat.renderQueue;
                #endregion

                #region Remove Duplicate Textures
                foreach (var name in new_mat.GetTexturePropertyNames()) 
                {
                    if (new_mat.GetTexture(name) == null)
                        continue;
                    if (new_mat.GetTexture(name).GetInstanceID() == original_tex.GetInstanceID())
                        new_mat.SetTexture(name, null);
                }
                #endregion

                AssetDatabase.CreateAsset(new_mat, encrypted_mat_path);
                Debug.LogFormat("{0} : create encrypted material : {1}", mat.name, AssetDatabase.GetAssetPath(new_mat));
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                var renderers = avatar.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers != null)
                {
                    for (int i = 0; i < renderers.Length; ++i)
                    {
                        var mats = renderers[i].sharedMaterials;
                        if (mats == null)
                            continue;
                        for (int j = 0; j < mats.Length; ++j)
                        {
                            if (mats[j] == null)
                                continue;
                            if (mats[j].GetInstanceID() == mat.GetInstanceID())
                            {
                                mats[j] = new_mat;
                                meshes.Add(renderers[i].gameObject);
                            }
                        }
                        renderers[i].sharedMaterials = mats;
                    }
                }
                var skinned_renderers = avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                if (skinned_renderers != null)
                {
                    for (int i = 0; i < skinned_renderers.Length; ++i)
                    {
                        var mats = skinned_renderers[i].sharedMaterials;
                        if (mats == null)
                            continue;
                        for (int j = 0; j < mats.Length; ++j)
                        {
                            if (mats[j] == null)
                                continue;
                            if (mats[j].GetInstanceID() == mat.GetInstanceID())
                            {
                                mats[j] = new_mat;
                                meshes.Add(skinned_renderers[i].gameObject);
                            }
                        }
                        skinned_renderers[i].sharedMaterials = mats;
                    }
                }
                //////////////////////////////////////////////////
            }
            EditorUtility.ClearProgressBar();

            ///////////////////////parameter////////////////////
            var av3 = avatar.GetComponent<VRC.SDK3.Avatars.Components.VRCAvatarDescriptor>();
            av3.expressionParameters = ParameterManager.AddKeyParameter(av3.expressionParameters, key_size);
            AssetDatabase.CreateAsset(av3.expressionParameters, asset_dir + "/" + gameObject.name + "/" + av3.expressionParameters.name + ".asset");

            ///////////////////////animator////////////////////
            var fx = AnimatorManager.DuplicateAnimator(av3.baseAnimationLayers[4].animatorController, Path.Combine(asset_dir, gameObject.name));
            av3.baseAnimationLayers[4].animatorController = fx;
            string animation_dir = Path.Combine(asset_dir, gameObject.name, "animations");

            GameObject[] mesh_array = new GameObject[meshes.Count];
            meshes.CopyTo(mesh_array);
            AnimatorManager.DuplicateAniamtions(Path.Combine(asset_dir, "Animations"), animation_dir, mesh_array);
            AnimatorManager.AddKeyLayer(fx, animation_dir, key_size, animation_speed);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ////////////////////////////////////////////////////

            gameObject.SetActive(false);
            var tester = avatar.AddComponent<ShellProtectorTester>();
            tester.lang = lang;
            tester.lang_idx = lang_idx;
            tester.protector = this;
            tester.user_key_length = key_size;
            Selection.activeObject = tester;
            DestroyImmediate(avatar.GetComponent<ShellProtector>());
        }

        public VRCExpressionParameters GetParameter()
        {
            var av3 = gameObject.GetComponent<VRCAvatarDescriptor>();
            if (av3 == null)
                return null;
            return av3.expressionParameters;
        }
    }
}
#endif