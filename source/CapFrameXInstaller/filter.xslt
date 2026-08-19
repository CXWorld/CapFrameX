<?xml version="1.0" encoding="UTF-8"?>
<xsl:stylesheet version="1.0"
xmlns:xsl="http://www.w3.org/1999/XSL/Transform"
xmlns:wix="http://schemas.microsoft.com/wix/2006/wi">
	<xsl:output method="xml" indent="yes" />
	<xsl:template match="@*|node()">
		<xsl:copy>
			<xsl:apply-templates select="@*|node()"/>
		</xsl:copy>
	</xsl:template>
	
	<!-- ### Adding the Win64-attribute to all Components -->
	<xsl:template match="wix:Component">
		<xsl:copy>
			<xsl:apply-templates select="@*" />
			<!-- Adding the Win64-attribute as we have a x64 application -->
			<xsl:attribute name="Win64">yes</xsl:attribute>

			<!-- Now take the rest of the inner tag -->
			<xsl:apply-templates select="node()" />
		</xsl:copy>
	</xsl:template>
	<!-- <xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.pdb')]" use="@Id" /> -->
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.xml')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.ilk')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.lib')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.exp')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.ini')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.iobj')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.ipdb')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.dll.metagen')]" use="@Id" />
	<!-- Extended.Wpf.Toolkit bundles AvalonDock, but CapFrameX only uses Xceed.Wpf.Toolkit. -->
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'Xceed.Wpf.AvalonDock')]" use="@Id" />
	<!-- The PMC reader is an optional plugin and must not be shipped with the core installer. -->
	<xsl:key name="pmcReaderPlugin" match="wix:Component[contains(wix:File/@Source, 'CapFrameX.PmcReader.Plugin.')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'CapFrameX.PmcReader.Plugin.')]" use="@Id" />
	<!-- Satellite assemblies bring their own copy of app.config, which nothing reads. The app's own
	     config is exempt: since the move to net9.0 it is named CapFrameX.dll.config instead of
	     CapFrameX.exe.config, and ConfigurationManager reads the update catalog and the webservice
	     endpoints from it, so dropping it disables those features in the installed build. -->
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.dll.config') and not(contains(wix:File/@Source, '\CapFrameX.dll.config'))]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, '.vshost.exe')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'app.config')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'app.config')]" use="@Id" />
	
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'OverlayEntryConfiguration_0.json')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'OverlayEntryConfiguration_1.json')]" use="@Id" />
	<xsl:key name="search" match="wix:Component[contains(wix:File/@Source, 'OverlayEntryConfiguration_2.json')]" use="@Id" />
	
	<!-- Drop the indentation belonging to the optional plugin as well, otherwise Heat leaves
	     whitespace-only lines in the generated fragment whenever the plugin is present. -->
	<xsl:template match="text()[not(normalize-space())][following-sibling::*[1][key('pmcReaderPlugin', @Id)]]" />
	<xsl:template match="wix:Component[key('search', @Id)]" />
	<xsl:template match="wix:ComponentRef[key('search', @Id)]"/>
	<xsl:template match="wix:Directory[key('search', @Id)]" />
</xsl:stylesheet>
