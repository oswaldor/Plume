-- INDEXES, DEFAULTS and CONSTRAINTS

ALTER TABLE [Topic] ADD CONSTRAINT [Topic$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]

ALTER TABLE [DocumentToTopicNormalized] ADD CONSTRAINT [DocumentToTopicNormalized$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[documentId] ASC,
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [DocumentToTopic] ADD CONSTRAINT [DocumentToTopic$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[documentId] ASC,
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [DocumentToTerm] ADD CONSTRAINT [DocumentToTerm$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[documentId] ASC,
	[termId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [TopicToTermNormalized] ADD CONSTRAINT [TopicToTermNormalized$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[termId] ASC,
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [TopicToTerm] ADD CONSTRAINT [TopicToTerm$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[termId] ASC,
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [Document] WITH CHECK ADD CONSTRAINT [Document$PrimaryKey] PRIMARY KEY CLUSTERED 
(
	[ID] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

ALTER TABLE [Term] WITH CHECK ADD  CONSTRAINT [Term$PrimaryKey] PRIMARY KEY CLUSTERED ([id] ASC) 
 WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [Document$testSet]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [Document$testSet] ON [dbo].[Document]
(
	[isPartOfTestSet] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [DocumentToTerm$documentId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTerm$documentId] ON [dbo].[DocumentToTerm]
(
	[documentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [DocumentToTerm$termId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTerm$termId] ON [dbo].[DocumentToTerm]
(
	[termId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopic$topicId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopic$topicId] ON [dbo].[DocumentToTopic]
(
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopic$documentId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopic$documentId] ON [dbo].[DocumentToTopic]
(
	[documentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopic$probability]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopic$probability] ON [dbo].[DocumentToTopic]
(
	[probability] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopicNormalized$topicId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopicNormalized$topicId] ON [dbo].[DocumentToTopicNormalized]
(
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopicNormalized$documentId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopicNormalized$documentId] ON [dbo].[DocumentToTopicNormalized]
(
	[documentId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [DocumentToTopicNormalized$probability]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [DocumentToTopicNormalized$probability] ON [dbo].[DocumentToTopicNormalized]
(
	[probability] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [Term$corpusFrequency]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [Term$corpusFrequency] ON [dbo].[Term]
(
	[corpusFrequency] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [Term$id]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [Term$id] ON [dbo].[Term]
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [Topic$documentCount]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [Topic$documentCount] ON [dbo].[Topic]
(
	[documentCount] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [Topic$id]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [Topic$id] ON [dbo].[Topic]
(
	[id] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO


CREATE NONCLUSTERED INDEX [Topic$good] ON [dbo].[Topic]
(
	[good] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO


CREATE NONCLUSTERED INDEX [Topic$allocations] ON [dbo].[Topic]
(
	[allocations] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

CREATE NONCLUSTERED INDEX [Topic$coherence] ON [dbo].[Topic]
(
	[coherence] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO


CREATE NONCLUSTERED INDEX [Topic$specificity] ON [dbo].[Topic]
(
	[specificity] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
	
CREATE NONCLUSTERED INDEX [Topic$distinctiveness] ON [dbo].[Topic]
(
	[distinctiveness] DESC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [TopicToTerm$clusterId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTerm$clusterId] ON [dbo].[TopicToTerm]
(
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [TopicToTerm$probability]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTerm$probability] ON [dbo].[TopicToTerm]
(
	[probability] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [TopicToTerm$termId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTerm$termId] ON [dbo].[TopicToTerm]
(
	[termId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO

/****** Object:  Index [TopicToTermNormalized$clusterId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTermNormalized$clusterId] ON [dbo].[TopicToTermNormalized]
(
	[topicId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [TopicToTermNormalized$probability]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTermNormalized$probability] ON [dbo].[TopicToTermNormalized]
(
	[probability] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [TopicToTermNormalized$TermClusterToTerm]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTermNormalized$TermClusterToTerm] ON [dbo].[TopicToTermNormalized]
(
	[termId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
/****** Object:  Index [TopicToTermNormalized$termId]    Script Date: 7/28/2014 10:34:13 AM ******/
CREATE NONCLUSTERED INDEX [TopicToTermNormalized$termId] ON [dbo].[TopicToTermNormalized]
(
	[termId] ASC
)WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]
GO
ALTER TABLE [dbo].[DocumentToTerm]  WITH CHECK ADD  CONSTRAINT [DocumentToTerm$DocumentDocumentToPhrase] FOREIGN KEY([documentId])
REFERENCES [dbo].[Document] ([ID])
GO
ALTER TABLE [dbo].[DocumentToTerm] CHECK CONSTRAINT [DocumentToTerm$DocumentDocumentToPhrase]
GO
ALTER TABLE [dbo].[DocumentToTerm]  WITH CHECK ADD  CONSTRAINT [DocumentToTerm$PhraseDocumentToPhrase] FOREIGN KEY([termId])
REFERENCES [dbo].[Term] ([id])
GO
ALTER TABLE [dbo].[DocumentToTerm] CHECK CONSTRAINT [DocumentToTerm$PhraseDocumentToPhrase]
GO
ALTER TABLE [dbo].[DocumentToTopicNormalized]  WITH CHECK ADD  CONSTRAINT [DocumentToTopicNormalized$ClusterDocumentToCluster] FOREIGN KEY([topicId])
REFERENCES [dbo].[Topic] ([id])
GO
ALTER TABLE [dbo].[DocumentToTopicNormalized] CHECK CONSTRAINT [DocumentToTopicNormalized$ClusterDocumentToCluster]
GO
ALTER TABLE [dbo].[DocumentToTopicNormalized]  WITH CHECK ADD  CONSTRAINT [DocumentToTopicNormalized$DocumentDocumentToCluster] FOREIGN KEY([documentId])
REFERENCES [dbo].[Document] ([ID])
GO
ALTER TABLE [dbo].[DocumentToTopicNormalized] CHECK CONSTRAINT [DocumentToTopicNormalized$DocumentDocumentToCluster]
GO

ALTER TABLE [dbo].[TopicToTerm]  WITH CHECK ADD  CONSTRAINT [TopicToTerm$ClusterClusterToTerm] FOREIGN KEY([topicId])
REFERENCES [dbo].[Topic] ([id])
GO
ALTER TABLE [dbo].[TopicToTerm] CHECK CONSTRAINT [TopicToTerm$ClusterClusterToTerm]
GO
ALTER TABLE [dbo].[TopicToTerm]  WITH CHECK ADD  CONSTRAINT [TopicToTerm$TermClusterToTerm] FOREIGN KEY([termId])
REFERENCES [dbo].[Term] ([id])
GO
ALTER TABLE [dbo].[TopicToTerm] CHECK CONSTRAINT [TopicToTerm$TermClusterToTerm]
GO
ALTER TABLE [dbo].[TopicToTermNormalized]  WITH CHECK ADD  CONSTRAINT [TopicToTermNormalized$ClusterClusterToTerm] FOREIGN KEY([topicId])
REFERENCES [dbo].[Topic] ([id])
GO
ALTER TABLE [dbo].[TopicToTermNormalized] CHECK CONSTRAINT [TopicToTermNormalized$ClusterClusterToTerm]
GO
ALTER TABLE [dbo].[TopicToTermNormalized]  WITH CHECK ADD CONSTRAINT [TopicToTermNormalized$TermClusterToTerm] FOREIGN KEY([termId])
REFERENCES [dbo].[Term] ([id])
GO
ALTER TABLE [dbo].[TopicToTermNormalized] CHECK CONSTRAINT [TopicToTermNormalized$TermClusterToTerm]
GO
