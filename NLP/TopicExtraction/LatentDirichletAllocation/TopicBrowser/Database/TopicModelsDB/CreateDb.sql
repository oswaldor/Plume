USE [master]
GO
/****** Object:  Database [TopicModels]    Script Date: 3/3/2015 10:01:11 AM ******/
CREATE DATABASE [TopicModels]
 CONTAINMENT = NONE
 ON  PRIMARY 
( NAME = N'TopicModels', FILENAME = N'C:\SQLServerData\MSSQL12.MSSQLSERVER\MSSQL\DATA\TopicModels.mdf' , SIZE = 102400KB , MAXSIZE = UNLIMITED, FILEGROWTH = 1024KB )
 LOG ON 
( NAME = N'TopicModels_log', FILENAME = N'C:\SQLServerData\MSSQL12.MSSQLSERVER\MSSQL\DATA\TopicModels_log.ldf' , SIZE = 102400KB , MAXSIZE = 2048GB , FILEGROWTH = 10%)
GO
ALTER DATABASE [TopicModels] SET COMPATIBILITY_LEVEL = 120
GO
IF (1 = FULLTEXTSERVICEPROPERTY('IsFullTextInstalled'))
begin
EXEC [TopicModels].[dbo].[sp_fulltext_database] @action = 'enable'
end
GO
ALTER DATABASE [TopicModels] SET ANSI_NULL_DEFAULT OFF 
GO
ALTER DATABASE [TopicModels] SET ANSI_NULLS OFF 
GO
ALTER DATABASE [TopicModels] SET ANSI_PADDING OFF 
GO
ALTER DATABASE [TopicModels] SET ANSI_WARNINGS OFF 
GO
ALTER DATABASE [TopicModels] SET ARITHABORT OFF 
GO
ALTER DATABASE [TopicModels] SET AUTO_CLOSE OFF 
GO
ALTER DATABASE [TopicModels] SET AUTO_SHRINK OFF 
GO
ALTER DATABASE [TopicModels] SET AUTO_UPDATE_STATISTICS ON 
GO
ALTER DATABASE [TopicModels] SET CURSOR_CLOSE_ON_COMMIT OFF 
GO
ALTER DATABASE [TopicModels] SET CURSOR_DEFAULT  GLOBAL 
GO
ALTER DATABASE [TopicModels] SET CONCAT_NULL_YIELDS_NULL OFF 
GO
ALTER DATABASE [TopicModels] SET NUMERIC_ROUNDABORT OFF 
GO
ALTER DATABASE [TopicModels] SET QUOTED_IDENTIFIER OFF 
GO
ALTER DATABASE [TopicModels] SET RECURSIVE_TRIGGERS OFF 
GO
ALTER DATABASE [TopicModels] SET  DISABLE_BROKER 
GO
ALTER DATABASE [TopicModels] SET AUTO_UPDATE_STATISTICS_ASYNC OFF 
GO
ALTER DATABASE [TopicModels] SET DATE_CORRELATION_OPTIMIZATION OFF 
GO
ALTER DATABASE [TopicModels] SET TRUSTWORTHY OFF 
GO
ALTER DATABASE [TopicModels] SET ALLOW_SNAPSHOT_ISOLATION OFF 
GO
ALTER DATABASE [TopicModels] SET PARAMETERIZATION SIMPLE 
GO
ALTER DATABASE [TopicModels] SET READ_COMMITTED_SNAPSHOT OFF 
GO
ALTER DATABASE [TopicModels] SET HONOR_BROKER_PRIORITY OFF 
GO
ALTER DATABASE [TopicModels] SET RECOVERY FULL 
GO
ALTER DATABASE [TopicModels] SET  MULTI_USER 
GO
ALTER DATABASE [TopicModels] SET PAGE_VERIFY CHECKSUM  
GO
ALTER DATABASE [TopicModels] SET DB_CHAINING OFF 
GO
ALTER DATABASE [TopicModels] SET FILESTREAM( NON_TRANSACTED_ACCESS = OFF ) 
GO
ALTER DATABASE [TopicModels] SET TARGET_RECOVERY_TIME = 0 SECONDS 
GO
ALTER DATABASE [TopicModels] SET DELAYED_DURABILITY = DISABLED 
GO
EXEC sys.sp_db_vardecimal_storage_format N'TopicModels', N'ON'
GO
USE [TopicModels]
GO
/****** Object:  User [redmond\yitan]    Script Date: 3/3/2015 10:01:11 AM ******/
CREATE USER [redmond\yitan] FOR LOGIN [REDMOND\yitan] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  User [redmond\rogerche]    Script Date: 3/3/2015 10:01:11 AM ******/
CREATE USER [redmond\rogerche] FOR LOGIN [REDMOND\rogerche] WITH DEFAULT_SCHEMA=[dbo]
GO
/****** Object:  DatabaseRole [TopicBrowserWrite]    Script Date: 3/3/2015 10:01:11 AM ******/
CREATE ROLE [TopicBrowserWrite]
GO
/****** Object:  DatabaseRole [TopicBrowserRead]    Script Date: 3/3/2015 10:01:11 AM ******/
CREATE ROLE [TopicBrowserRead]
GO
ALTER ROLE [TopicBrowserRead] ADD MEMBER [redmond\yitan]
GO
ALTER ROLE [TopicBrowserWrite] ADD MEMBER [redmond\yitan]
GO
ALTER ROLE [db_owner] ADD MEMBER [redmond\yitan]
GO
ALTER ROLE [TopicBrowserRead] ADD MEMBER [redmond\rogerche]
GO
ALTER ROLE [TopicBrowserWrite] ADD MEMBER [redmond\rogerche]
GO
ALTER ROLE [TopicBrowserRead] ADD MEMBER [TopicBrowserWrite]
GO
/****** Object:  Table [dbo].[corpusSample]    Script Date: 3/3/2015 10:01:11 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[corpusSample](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[culture] [nvarchar](50) NOT NULL,
	[corpus] [nvarchar](50) NOT NULL,
	[sample] [nvarchar](50) NOT NULL,
	[documentCount] [int] NULL,
	[wordCount] [int] NULL,
	[minWordDocumentFrequency] [int] NULL,
	[maxRelativeWordDocumentFrequency] [float] NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[models]    Script Date: 3/3/2015 10:01:11 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[models](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[corpusSampleId] [int] NOT NULL,
	[topicCount] [int] NOT NULL,
	[alpha] [float] NOT NULL,
	[rho] [float] NOT NULL,
	[miniBatch] [int] NOT NULL,
	[passes] [int] NOT NULL,
	[initialT] [float] NOT NULL,
	[powerT] [float] NOT NULL,
	[goodTopicCount] [int] NOT NULL,
	[avgTopicCoherence] [real] NULL,
	[avgTopicSpecificity] [real] NULL,
	[avgTopicDistinctiveness] [real] NULL,
	[serverName] [nvarchar](max) NOT NULL,
	[databaseName] [nvarchar](max) NULL,
	[avgGoodTopicCoherence] [float] NULL,
	[avgGoodTopicSpecificity] [float] NULL,
	[avgGoodTopicDistinctiveness] [float] NULL,
	[stdDevGoodTopicSpecificity] [float] NULL,
	[stdDevGoodTopicDistinctiveness] [float] NULL,
	[entropyGoodTopicCoherence] [float] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
/****** Object:  View [dbo].[ModelMetrics]    Script Date: 3/3/2015 10:01:11 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


CREATE VIEW [dbo].[ModelMetrics]
AS
SELECT     dbo.corpusSample.culture, dbo.corpusSample.corpus, dbo.corpusSample.sample, dbo.corpusSample.minWordDocumentFrequency, 
			dbo.corpusSample.maxRelativeWordDocumentFrequency, dbo.models.topicCount, 
			(dbo.models.topicCount - dbo.models.goodTopicCount) AS badTopicCount, dbo.models.alpha, 
                  dbo.models.rho, dbo.models.miniBatch, dbo.models.passes, dbo.models.initialT, dbo.models.powerT, dbo.models.avgTopicCoherence, dbo.models.avgTopicSpecificity, dbo.models.avgTopicDistinctiveness, dbo.models.avgGoodTopicCoherence, 
                  dbo.models.avgGoodTopicSpecificity, dbo.models.avgGoodTopicDistinctiveness, dbo.models.stdDevGoodTopicSpecificity, dbo.models.stdDevGoodTopicDistinctiveness, dbo.models.entropyGoodTopicCoherence
FROM        dbo.corpusSample INNER JOIN
                  dbo.models ON dbo.corpusSample.id = dbo.models.corpusSampleId



GO
/****** Object:  StoredProcedure [dbo].[spAddModel]    Script Date: 3/3/2015 10:01:11 AM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO


-- =============================================
-- Author:		<Author,,Name>
-- Create date: <Create Date,,>
-- Description:	<Description,,>
-- =============================================
CREATE PROCEDURE [dbo].[spAddModel] 
    /* Corpus attributes */
	@culture nvarchar(10),
    @corpus nvarchar(MAX),
    @sample nvarchar(MAX),
    @documentCount int,
    @wordCount int,
    @minWordDocumentFrequency int,
    @maxRelativeWordDocumentFrequency float,

	/* Model Parameters */
    @topicCount int,
	@alpha float,
    @rho float,
    @minibatch int,
    @passes int,
    @initialT float,
    @powerT float,

	/* Model metrics */
    @goodTopicCount int,
    @avgTopicCoherence float,
    @avgTopicSpecificity float,
    @avgTopicDistinctiveness float,
	@avgGoodTopicCoherence  float,
	@avgGoodTopicSpecificity  float,
	@avgGoodTopicDistinctiveness  float,
	@stdDevGoodTopicSpecificity  float,
	@stdDevGoodTopicDistinctiveness  float,
	@entropyGoodTopicCoherence  float,

	@dbName nvarchar(250) OUT,
	@modelId int out
AS
BEGIN
	SET NOCOUNT ON;

	DECLARE @corpusSampleId int
	
	/* Test to see if the corpus sample already exists */
	SELECT @corpusSampleId = id
	FROM corpusSample 
	WHERE corpusSample.culture = @culture 
		AND corpusSample.corpus = @corpus
		AND corpusSample.sample = @sample
		AND corpusSample.minWordDocumentFrequency = @minWordDocumentFrequency
		AND corpusSample.maxRelativeWordDocumentFrequency = @maxRelativeWordDocumentFrequency

	/* If it doesn't add it */
	IF ISNULL(@corpusSampleId, -1) < 0 
	BEGIN
		INSERT INTO corpusSample(culture, corpus, sample, documentCount, wordCount, minWordDocumentFrequency, maxRelativeWordDocumentFrequency)
		VALUES (@culture, @corpus, @sample, @documentCount, @wordCount, @minWordDocumentFrequency, @maxRelativeWordDocumentFrequency)
		SET @corpusSampleId =  @@IDENTITY 
	END

	/* Test to see if we already have a db for this model */
	SELECT @modelId = id, @dbName = models.databaseName
	FROM models 
	WHERE	models.corpusSampleId = @corpusSampleId
		AND models.topicCount = @topicCount
		AND models.alpha = @alpha
		AND models.rho = @rho
		AND models.passes = @passes
		AND models.minibatch = @minibatch
		AND models.initialT = @initialT
		AND models.powerT = @powerT

	IF ISNULL(@modelId, -1) < 0 
	BEGIN
		SET @dbName = @culture + '.' + @corpus + '.' + @sample + CONVERT(NVARCHAR(MAX), @minWordDocumentFrequency) + '-' + CONVERT(NVARCHAR(MAX), @maxRelativeWordDocumentFrequency)
		INSERT INTO models(topicCount, corpusSampleId, alpha, rho, passes, minibatch, initialT, powerT, goodTopicCount, avgTopicCoherence, avgTopicSpecificity, avgTopicDistinctiveness, avgGoodTopicCoherence, avgGoodTopicSpecificity, avgGoodTopicDistinctiveness, stdDevGoodTopicSpecificity,stdDevGoodTopicDistinctiveness, entropyGoodTopicCoherence, serverName ,databaseName)
		VALUES (@topicCount, @corpusSampleId, @alpha, @rho, @passes, @minibatch, @initialT, @powerT, @goodTopicCount, @avgTopicCoherence, @avgTopicSpecificity, @avgTopicDistinctiveness, 
			@avgGoodTopicCoherence, @avgGoodTopicSpecificity, @avgGoodTopicDistinctiveness, @stdDevGoodTopicSpecificity,@stdDevGoodTopicDistinctiveness, @entropyGoodTopicCoherence, '', @dbName)
		SET @modelId =  @@IDENTITY
	END

END



GO
EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPane1', @value=N'[0E232FF0-B466-11cf-A24F-00AA00A3EFFF, 1.00]
Begin DesignProperties = 
   Begin PaneConfigurations = 
      Begin PaneConfiguration = 0
         NumPanes = 4
         Configuration = "(H (1[41] 4[30] 2[10] 3) )"
      End
      Begin PaneConfiguration = 1
         NumPanes = 3
         Configuration = "(H (1 [50] 4 [25] 3))"
      End
      Begin PaneConfiguration = 2
         NumPanes = 3
         Configuration = "(H (1 [50] 2 [25] 3))"
      End
      Begin PaneConfiguration = 3
         NumPanes = 3
         Configuration = "(H (4 [30] 2 [40] 3))"
      End
      Begin PaneConfiguration = 4
         NumPanes = 2
         Configuration = "(H (1 [56] 3))"
      End
      Begin PaneConfiguration = 5
         NumPanes = 2
         Configuration = "(H (2 [66] 3))"
      End
      Begin PaneConfiguration = 6
         NumPanes = 2
         Configuration = "(H (4 [50] 3))"
      End
      Begin PaneConfiguration = 7
         NumPanes = 1
         Configuration = "(V (3))"
      End
      Begin PaneConfiguration = 8
         NumPanes = 3
         Configuration = "(H (1[56] 4[18] 2) )"
      End
      Begin PaneConfiguration = 9
         NumPanes = 2
         Configuration = "(H (1 [75] 4))"
      End
      Begin PaneConfiguration = 10
         NumPanes = 2
         Configuration = "(H (1[66] 2) )"
      End
      Begin PaneConfiguration = 11
         NumPanes = 2
         Configuration = "(H (4 [60] 2))"
      End
      Begin PaneConfiguration = 12
         NumPanes = 1
         Configuration = "(H (1) )"
      End
      Begin PaneConfiguration = 13
         NumPanes = 1
         Configuration = "(V (4))"
      End
      Begin PaneConfiguration = 14
         NumPanes = 1
         Configuration = "(V (2))"
      End
      ActivePaneConfig = 0
   End
   Begin DiagramPane = 
      Begin Origin = 
         Top = 0
         Left = 0
      End
      Begin Tables = 
         Begin Table = "corpusSample"
            Begin Extent = 
               Top = 61
               Left = 782
               Bottom = 224
               Right = 1131
            End
            DisplayFlags = 280
            TopColumn = 4
         End
         Begin Table = "models"
            Begin Extent = 
               Top = 7
               Left = 445
               Bottom = 326
               Right = 730
            End
            DisplayFlags = 280
            TopColumn = 10
         End
      End
   End
   Begin SQLPane = 
   End
   Begin DataPane = 
      Begin ParameterDefaults = ""
      End
   End
   Begin CriteriaPane = 
      Begin ColumnWidths = 11
         Column = 2460
         Alias = 900
         Table = 1170
         Output = 720
         Append = 1400
         NewValue = 1170
         SortType = 1350
         SortOrder = 1410
         GroupBy = 1350
         Filter = 1350
         Or = 1350
         Or = 1350
         Or = 1350
      End
   End
End
' , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'ModelMetrics'
GO
EXEC sys.sp_addextendedproperty @name=N'MS_DiagramPaneCount', @value=1 , @level0type=N'SCHEMA',@level0name=N'dbo', @level1type=N'VIEW',@level1name=N'ModelMetrics'
GO
USE [master]
GO
ALTER DATABASE [TopicModels] SET  READ_WRITE 
GO
