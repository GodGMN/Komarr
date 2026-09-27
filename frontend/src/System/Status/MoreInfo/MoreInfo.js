import React, { Component } from 'react';
import DescriptionList from 'Components/DescriptionList/DescriptionList';
import DescriptionListItemDescription from 'Components/DescriptionList/DescriptionListItemDescription';
import DescriptionListItemTitle from 'Components/DescriptionList/DescriptionListItemTitle';
import FieldSet from 'Components/FieldSet';
import Link from 'Components/Link/Link';
import translate from 'Utilities/String/translate';

class MoreInfo extends Component {

  //
  // Render

  render() {
    return (
      <FieldSet legend={translate('MoreInfo')}>
        <DescriptionList>
          <DescriptionListItemTitle>Home page</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/Rorqualx/Librarr">github.com/Rorqualx/Librarr</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Source</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/Rorqualx/Librarr">github.com/Rorqualx/Librarr</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Bugs &amp; Feature Requests</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/Rorqualx/Librarr/issues">github.com/Rorqualx/Librarr/issues</Link>
          </DescriptionListItemDescription>

          <DescriptionListItemTitle>Questions &amp; Support</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://github.com/Rorqualx/Librarr/discussions">github.com/Rorqualx/Librarr/discussions</Link>
          </DescriptionListItemDescription>

          {/*
            Upstream's wiki. Librarr is a fork of Readarr and most operational
            topics -- logging, remote path mappings, connection settings -- are
            unchanged, so this is still the best documentation for them. Treat
            anything it says about metadata sources as obsolete: that is the
            part the fork replaced.
          */}
          <DescriptionListItemTitle>Wiki (upstream Readarr)</DescriptionListItemTitle>
          <DescriptionListItemDescription>
            <Link to="https://wiki.servarr.com/readarr">wiki.servarr.com/readarr</Link>
          </DescriptionListItemDescription>

        </DescriptionList>
      </FieldSet>
    );
  }
}

MoreInfo.propTypes = {

};

export default MoreInfo;
