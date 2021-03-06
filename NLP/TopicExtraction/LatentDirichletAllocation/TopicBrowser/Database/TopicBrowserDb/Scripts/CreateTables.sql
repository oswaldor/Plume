/****** Object:  Table [dbo].[Document]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Document](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[uri] [nvarchar](max) NULL,
	[title] [nvarchar](max) NULL,
	[isPartOfTestSet] [bit] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
/****** Object:  Table [dbo].[DocumentToTerm]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentToTerm](
	[documentId] [int] NOT NULL,
	[termId] [int] NOT NULL,
	[frequency] [int] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[DocumentToTopic]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentToTopic](
	[documentId] [int] NOT NULL,
	[topicId] [int] NOT NULL,
	[probability] [float] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[DocumentToTopicNormalized]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[DocumentToTopicNormalized](
	[documentId] [int] NOT NULL,
	[topicId] [int] NOT NULL,
	[allocation] [float] NOT NULL,
	[probability] [float] NOT NULL,
	[unitVectorProbability] [float] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[SimilarDocument]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[SimilarDocument](
	[documentId] [int] NOT NULL,
	[similarDocumentId] [int] NOT NULL,
	[similarity] [float] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[Term]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Term](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[term] [nvarchar](max) NOT NULL,
	[corpusFrequency] [int] NULL,
	[documentFrequency] [int] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
/****** Object:  Table [dbo].[Topic]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[Topic](
	[id] [int] IDENTITY(1,1) NOT NULL,
	[label] [nvarchar](max) NULL,
	[documentCount] [int] NULL,
	[good] [tinyint] NULL,
	[allocations] [float] NULL,
	[coherence] [float] NULL,
	[specificity] [float] NULL,
	[distinctiveness] [float] NULL
) ON [PRIMARY] TEXTIMAGE_ON [PRIMARY]

GO
/****** Object:  Table [dbo].[TopicToTerm]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TopicToTerm](
	[topicId] [int] NOT NULL,
	[termId] [int] NOT NULL,
	[probability] [float] NOT NULL
) ON [PRIMARY]

GO
/****** Object:  Table [dbo].[TopicToTermNormalized]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE TABLE [dbo].[TopicToTermNormalized](
	[topicId] [int] NOT NULL,
	[termId] [int] NOT NULL,
	[probability] [float] NOT NULL,
	[unitVectorProbability] [float] NULL
) ON [PRIMARY]

GO
/****** Object:  View [dbo].[viewDocumentVectorL1]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[viewDocumentVectorL1]
AS
	SELECT 	documentId, SUM(probability) AS vectorLength
	FROM DocumentToTopic
	GROUP BY documentId 

GO
/****** Object:  View [dbo].[viewDocumentVectorLength]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO
CREATE VIEW [dbo].[viewDocumentVectorLength]
AS
	SELECT 	documentId, SQRT(SUM(probability*probability)) AS vectorLength
	FROM DocumentToTopic
	GROUP BY documentId 

GO
/****** Object:  View [dbo].[viewTermVectorLength]    Script Date: 9/15/2014 4:11:01 PM ******/
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

CREATE VIEW [dbo].[viewTermVectorLength]
AS
	SELECT termId, SUM(probability) AS vectorLength, SQRT(SUM(probability*probability)) as unitVectorProbability
	FROM TopicToTerm
	GROUP BY termId 


GO


/* GRANT permissions to Tables and views to two roles: TopicBrowserRead and TopicBrowserWrite */

GRANT SELECT ON [dbo].[Document] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[Document] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[Document] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[Document] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[DocumentToTerm] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[DocumentToTerm] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[DocumentToTerm] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[DocumentToTerm] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[DocumentToTopic] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[DocumentToTopic] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[DocumentToTopic] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[DocumentToTopic] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[DocumentToTopicNormalized] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[DocumentToTopicNormalized] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[DocumentToTopicNormalized] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[DocumentToTopicNormalized] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[SimilarDocument] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[SimilarDocument] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[SimilarDocument] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT SELECT ON [dbo].[SimilarDocument] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[SimilarDocument] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[Term] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[Term] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[Term] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[Term] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[Topic] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[Topic] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[Topic] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[Topic] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[TopicToTerm] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[TopicToTerm] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[TopicToTerm] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[TopicToTerm] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[TopicToTermNormalized] TO [TopicBrowserRead] AS [dbo]
GO
GRANT DELETE ON [dbo].[TopicToTermNormalized] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT INSERT ON [dbo].[TopicToTermNormalized] TO [TopicBrowserWrite] AS [dbo]
GO
GRANT UPDATE ON [dbo].[TopicToTermNormalized] TO [TopicBrowserWrite] AS [dbo]
GO

GRANT SELECT ON [dbo].[viewTermVectorLength] TO [TopicBrowserRead] AS [dbo]
GO

GRANT SELECT ON [dbo].[viewDocumentVectorLength] TO [TopicBrowserRead] AS [dbo]
GO

GRANT SELECT ON [dbo].[viewDocumentVectorL1] TO [TopicBrowserRead] AS [dbo]
GO
