import React from 'react';
import FieldSet from 'Components/FieldSet';
import PageContent from 'Components/Page/PageContent';
import PageContentBody from 'Components/Page/PageContentBody';
import translate from 'Utilities/String/translate';

function Quality() {
  return (
    <PageContent title={translate('QualitySettings')}>
      <PageContentBody>
        <FieldSet legend={translate('QualitySettings')}>
          <p>{translate('MangaQualityUnavailable')}</p>
          <p>{translate('MangaQualityPlanned')}</p>
        </FieldSet>
      </PageContentBody>
    </PageContent>
  );
}

export default Quality;
