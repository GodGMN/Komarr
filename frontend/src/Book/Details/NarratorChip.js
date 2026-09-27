import PropTypes from 'prop-types';
import React from 'react';
import Label from 'Components/Label';
import Link from 'Components/Link/Link';
import { kinds, sizes } from 'Helpers/Props';

// Renders one narrator as a Label chip. Phase 12.4 wrapped this in a
// router Link to /narrator/:id once the per-narrator detail page
// landed. If `id` is missing or zero we fall back to a plain chip so
// stale payloads or test stubs don't render a dead link.
function NarratorChip({ id, name }) {
  const chip = (
    <Label
      kind={kinds.INFO}
      size={sizes.SMALL}
    >
      {name}
    </Label>
  );

  if (!id) {
    return chip;
  }

  return (
    <Link to={`/narrator/${id}`}>
      {chip}
    </Link>
  );
}

NarratorChip.propTypes = {
  id: PropTypes.number,
  name: PropTypes.string.isRequired
};

export default NarratorChip;
