<?xml version="1.0" encoding="utf-8"?>
<xsl:stylesheet version="1.0" xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
    xmlns:msxsl="urn:schemas-microsoft-com:xslt" exclude-result-prefixes="msxsl"
                xmlns:tpl="urn:templates"
>
  <xsl:param name="AssetBundleNamesClass"/>


  <xsl:output method="text" indent="yes"/>
  <xsl:key match="Asset" use="@Class" name="ClassKey"/>
  <xsl:template match="/">
    <xsl:value-of select ="tpl:Set('OutputPath','project://Temp/gen/AssetBundleNames.cs')"/>
    <xsl:text disable-output-escaping="no">/**********  自动生成文件 **********
 * 不要手动修改.
 * 菜单: Build/AssetBundle/gen AssetBundleNames
 ***********************************/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class </xsl:text>
    <xsl:value-of select="$AssetBundleNamesClass"/>
    <xsl:text>
{
</xsl:text>
    <xsl:apply-templates/>
    <xsl:text>
    public class AssetBundleAttribute : Attribute
    {
        public bool Preloaded { get; set; }
        public string[] Tags { get; set; }
    }

    public class AssetAttribute : Attribute
    {
        public string Id { get; set; }
        public string[] Components { get; set; }
        public string AssetType { get; set; }
    }

    public static IEnumerable&lt;FieldInfo&gt; GetAllAssetBundleFields()
    {
        foreach (var field in typeof(AssetBundleNames).GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
        {
            var abAttr = field.GetCustomAttributes(typeof(AssetBundleAttribute), false).FirstOrDefault();
            if (abAttr == null)
                continue;
            yield return field;
        }
    }

    public static IEnumerable&lt;string&gt; GetAllAssetBundleNames()
    {
        foreach (var field in GetAllAssetBundleFields())
        {
            yield return field.GetValue(null) as string;
        }
    }
    public static IEnumerable&lt;string&gt; GetPreloadedAssetBundleNames()
    {
        foreach (var field in GetAllAssetBundleFields())
        {
            var attr = field.GetCustomAttributes(typeof(AssetBundleAttribute), false)[0] as AssetBundleAttribute;
            if (attr.Preloaded)
                yield return field.GetValue(null) as string;
        }
    }


    public static IEnumerable&lt;FieldInfo&gt; GetAllAssetFields()
    {
        var type = typeof(</xsl:text>
    <xsl:value-of select="$AssetBundleNamesClass"/>
    <xsl:text>);
        foreach (var field in new Type[] { type }.Concat(type.GetNestedTypes())
            .SelectMany(o => o.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
        {
            var assetAttr = field.GetCustomAttributes(typeof(AssetAttribute), false).FirstOrDefault();
            if (assetAttr == null)
                continue;
            yield return field;
        }
    }

    public static IEnumerable&lt;string[]&gt; GetAllAssetNames()
    {
        foreach (var field in GetAllAssetFields())
            yield return field.GetValue(null) as string[];
    }
    
    private static Dictionary&lt;AssetKey, string[]&gt; cachedAssetNames;

    private static IEnumerable&lt;Type&gt; EnumerateTypes(Type type)
    {       
        if ( type==null)
            type =typeof( Object);
        yield return type;
        if (type != typeof(Object))
            yield return typeof(Object);
    }

    private static void CacheAssetNames()
    {
        if (cachedAssetNames != null)
            return;
        var assetNames = new Dictionary&lt;AssetKey, string[]&gt;();

        Type assetType;
        string[] assetNameAndBundle;
        string assetName, fileName, fileNoExtension;
        Dictionary&lt;string, Type&gt; types = new Dictionary&lt;string, Type&gt;();
        var assetBundleNamesType = typeof(AssetBundleNames);
        foreach (var field in new Type[] { assetBundleNamesType }.Concat(assetBundleNamesType.GetNestedTypes())
            .SelectMany(o => o.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)))
        {
            var assetAttr = field.GetCustomAttributes(typeof(AssetAttribute), false).FirstOrDefault() as AssetAttribute;
            if (assetAttr == null)
                continue;

            if (string.IsNullOrEmpty(assetAttr.AssetType))
                assetType = typeof(Object);
            else
            {
                if (!types.TryGetValue(assetAttr.AssetType, out assetType))
                {
                    assetType = Type.GetType(assetAttr.AssetType);
                    if (assetType == null)
                        assetType = typeof(UnityEngine.Object).Assembly.GetType("UnityEngine." + assetAttr.AssetType);
                    if (assetType == null)
                        assetType = typeof(Object);
                    types[assetAttr.AssetType] = assetType;
                }
            }
            if (assetType == null)
                continue;

            assetNameAndBundle = field.GetValue(null) as string[];
            assetName = assetNameAndBundle[1];
            fileName = Path.GetFileName(assetName);
              fileNoExtension = Path.GetFileNameWithoutExtension(fileName);

            foreach (var type in EnumerateTypes(assetType))
            {
                assetNames[new AssetKey(assetName, type)] = assetNameAndBundle;
                assetNames[new AssetKey(fileName, type)] = assetNameAndBundle;
                assetNames[new AssetKey(fileNoExtension, type)] = assetNameAndBundle;
            }
        }

        cachedAssetNames = assetNames;
    }


   public static string[] GetAssetName(string assetName, Type assetType = null)
    {

        if (assetType == null)
            assetType = typeof(UnityEngine.Object);

        var key = new AssetKey(assetName, assetType);
        if (cachedAssetNames == null)
        {
            CacheAssetNames();
        }
        string[] assetNameAndBundle;
        if(!cachedAssetNames.TryGetValue(key,out assetNameAndBundle))
        {
            Debug.LogError("not asset name: " + assetName + ", type: " + assetType);
            return null;
        }
        return assetNameAndBundle;
    }

   public static string[] GetAssetName&lt;T&gt;(string assetName)
    {
        return GetAssetName(assetName, typeof(T));
    }



    struct AssetKey : IEquatable&lt;AssetKey&gt;
    {
        public Type AssetType;
        public string AssetName;

        public AssetKey(string assetName, Type assetType)
        {
            this.AssetName = assetName;
            this.AssetType = assetType;
        }

        public override bool Equals(object obj)
        {
            return Equals((AssetKey)obj);
        }
        public bool Equals(AssetKey other)
        {
        //    if (other == null)
           //     return false;
            return AssetType== other.AssetType &amp;&amp;
                object.Equals(AssetName, other.AssetName);
        }
        public override int GetHashCode()
        {
            int hash = AssetType.GetHashCode();
            hash = CombineHashCodes(hash, AssetName.GetHashCode());
            return hash;
        }
        static int CombineHashCodes(int h1, int h2)
        {
            return h1 * 31 + h2;
        }
    }

}</xsl:text>
  </xsl:template>

  <xsl:template match="AssetBundles">
    <xsl:apply-templates/>
    <xsl:text>
</xsl:text>
    <xsl:for-each select="AssetBundle/Asset[@Class=$AssetBundleNamesClass]">
      <xsl:text>    </xsl:text>
      <xsl:call-template name="assetAttribute"/>
      <xsl:text>    </xsl:text>
      <xsl:call-template name="assetMember"/>
    </xsl:for-each>
    <xsl:text>
</xsl:text>
    <xsl:for-each select="//Asset[generate-id()=generate-id(key('ClassKey',@Class))]">
      <xsl:if test="@Class!=$AssetBundleNamesClass">
        <xsl:text>
    public sealed class </xsl:text>
        <xsl:value-of select="@Class"/>
        <xsl:text>
    {
</xsl:text>
        <xsl:for-each select="key('ClassKey',@Class)">
          <xsl:text>        </xsl:text>
          <xsl:call-template name="assetAttribute"/>
          <xsl:text>        </xsl:text>
          <xsl:call-template name="assetMember"/>
        </xsl:for-each>
        <xsl:text>
    }
</xsl:text>
      </xsl:if>
    </xsl:for-each>
  </xsl:template>

  <xsl:template match="AssetBundle">
    <xsl:text>
    [AssetBundle(Preloaded = </xsl:text><xsl:value-of select="@Preloaded"/>
    <xsl:if test="count(Tags/Tag)>0">
      <xsl:text>, Tags = new string[] {</xsl:text>
      <xsl:for-each  select="Tags/Tag">
        <xsl:text> "</xsl:text>
        <xsl:value-of select="text()"/>
        <xsl:text>"</xsl:text>
        <xsl:if test="position()!=last()">,</xsl:if>
      </xsl:for-each>
      <xsl:text> }</xsl:text>
      </xsl:if>
    <xsl:text>)]
    public static readonly string </xsl:text><xsl:value-of select="@Field"/> = "<xsl:value-of select="@FieldValue"/><xsl:text>";</xsl:text>
  </xsl:template>
  <xsl:template name="assetMember">
    <xsl:text>public static readonly string[] </xsl:text><xsl:value-of select="@Field"/> = new string[] { <xsl:value-of select="../@Field"/>, "<xsl:value-of select="@FieldValue"/>"<xsl:text> };
</xsl:text>
  </xsl:template>
  <xsl:template name="assetAttribute">

    <xsl:text>[Asset(</xsl:text>
    <xsl:if test="Components/Component = 'NetworkIdentity'">
      <xsl:text>Id = "</xsl:text>
      <xsl:value-of select="@Id"/>
      <xsl:text>", </xsl:text>
    </xsl:if>
    <xsl:text>AssetType = "</xsl:text>
    <xsl:value-of select="@Type"/>
    <xsl:text>"</xsl:text>
    <xsl:if test="count(Components/Component)>0">
      <xsl:text>, Components = new string[] {</xsl:text>
      <xsl:for-each  select="Components/Component">
        <xsl:text> "</xsl:text>
        <xsl:value-of select="text()"/>
        <xsl:text>"</xsl:text>
        <xsl:if test="position()!=last()">,</xsl:if>
      </xsl:for-each>
      <xsl:text> }</xsl:text>
    </xsl:if>
    <xsl:text>)]
</xsl:text>
  </xsl:template>
</xsl:stylesheet>
