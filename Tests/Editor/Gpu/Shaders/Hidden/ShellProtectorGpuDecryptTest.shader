Shader "Hidden/ShellProtectorGpuDecryptTest"
{
    Properties
    {
        _MainTex ("Main", 2D) = "white" {}
        _MipTex ("Mip", 2D) = "black" {}
        _EncryptTex0 ("Encrypted0", 2D) = "white" {}
        _EncryptTex1 ("Encrypted1", 2D) = "white" {}
        _Key0 ("key0", Float) = 0
        _Key1 ("key1", Float) = 0
        _Key2 ("key2", Float) = 0
        _Key3 ("key3", Float) = 0
        _Key4 ("key4", Float) = 0
        _Key5 ("key5", Float) = 0
        _Key6 ("key6", Float) = 0
        _Key7 ("key7", Float) = 0
        _Key8 ("key8", Float) = 0
        _Key9 ("key9", Float) = 0
        _Key10 ("key10", Float) = 0
        _Key11 ("key11", Float) = 0
        _Key12 ("key12", Float) = 0
        _Key13 ("key13", Float) = 0
        _Key14 ("key14", Float) = 0
        _Key15 ("key15", Float) = 0
        _Woffset ("Woffset", Integer) = 0
        _Hoffset ("Hoffset", Integer) = 0
        _HashMagic ("HashMagic", Integer) = 0
        _PasswordHash ("PasswordHash", Integer) = 0
        _Nonce0 ("Nonce0", Integer) = 0
        _Nonce1 ("Nonce1", Integer) = 0
        _Nonce2 ("Nonce2", Integer) = 0
        _Rounds ("Rounds", Integer) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "Reference"
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;

            half3 ShellProtectorGammaCorrection(half3 rgb)
            {
                return rgb * rgb * (rgb * (half)0.2 + (half)0.8);
            }

            half4 frag(v2f_img i) : SV_Target
            {
                half4 color = tex2D(_MainTex, i.uv);
                return half4(ShellProtectorGammaCorrection(color.rgb), color.a);
            }
            ENDCG
        }

        Pass
        {
            Name "DecryptBox"
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
            #pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
            #pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
            #pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            #include "UnityCG.cginc"

            Texture2D _EncryptTex0;
            Texture2D _EncryptTex1;
            Texture2D _MipTex;
            SamplerState point_repeat_sampler;
            half4 _EncryptTex0_TexelSize;
            int _PasswordHash;

            #include "../../../../../Runtime/Shader/ShellProtector.cginc"

            half4 frag(v2f_img i) : SV_Target
            {
                return DecryptTextureBox(_EncryptTex0, _EncryptTex1, point_repeat_sampler, _EncryptTex0_TexelSize, _MipTex, point_repeat_sampler, i.uv);
            }
            ENDCG
        }

        Pass
        {
            Name "DecryptBilinear"
            CGPROGRAM
            #pragma target 5.0
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma shader_feature_local _SHELL_PROTECTOR_XXTEA
            #pragma shader_feature_local _SHELL_PROTECTOR_CHACHA
            #pragma shader_feature_local _SHELL_PROTECTOR_FORMAT0
            #pragma shader_feature_local _SHELL_PROTECTOR_FORMAT1
            #include "UnityCG.cginc"

            Texture2D _EncryptTex0;
            Texture2D _EncryptTex1;
            Texture2D _MipTex;
            SamplerState point_repeat_sampler;
            half4 _EncryptTex0_TexelSize;
            int _PasswordHash;

            #include "../../../../../Runtime/Shader/ShellProtector.cginc"

            half4 frag(v2f_img i) : SV_Target
            {
                return DecryptTextureBilinear(_EncryptTex0, _EncryptTex1, point_repeat_sampler, _EncryptTex0_TexelSize, _MipTex, point_repeat_sampler, i.uv);
            }
            ENDCG
        }
    }
}
