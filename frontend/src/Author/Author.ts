import ModelBase from 'App/ModelBase';

export type AuthorStatus = 'continuing' | 'ended';

interface Author extends ModelBase {
  added: string;
  genres: string[];
  monitored: boolean;
  overview: string;
  path: string;
  qualityProfileId: number;

  // 0 means this author has no separate audiobook profile; audiobooks are
  // ranked by qualityProfileId like everything else.
  audiobookQualityProfileId: number;
  metadataProfileId: number;
  rootFolderPath: string;
  sortName: string;
  status: AuthorStatus;
  tags: number[];
  authorName: string;
  isSaving?: boolean;
}

export default Author;
