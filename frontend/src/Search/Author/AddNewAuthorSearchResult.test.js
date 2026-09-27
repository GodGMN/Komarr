import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';
import AddNewAuthorSearchResult from './AddNewAuthorSearchResult';

// Open Library carries several author records for most well-known names, and
// they are identical on every field this card shows except the work count --
// searching "Tolkien" returns J.R.R. Tolkien with 355 works alongside a
// "Tolkien" stub with 1. The count is the only thing on the card that tells
// them apart, so these pin that it is actually rendered.

function renderResult(props = {}) {
  return render(
    <AddNewAuthorSearchResult
      foreignAuthorId="OL26320A"
      titleSlug="OL26320A"
      authorName="J. R. R. Tolkien"
      status="ended"
      ratings={{ votes: 0, value: 0 }}
      folder="J. R. R. Tolkien"
      images={[]}
      isExistingAuthor={false}
      isSmallScreen={true}
      {...props}
    />
  );
}

describe('AddNewAuthorSearchResult', () => {
  it('shows the work count', () => {
    renderResult({ workCount: 355 });

    expect(screen.getByText('AuthorWorkCount(count=355)')).toBeInTheDocument();
  });

  // The whole point: a stub record has to look different from a real one.
  it('calls out an author with no works', () => {
    renderResult({ workCount: 0 });

    expect(screen.getByText('AuthorNoWorks')).toBeInTheDocument();
    expect(screen.queryByText(/AuthorWorkCount/)).not.toBeInTheDocument();
  });

  // A response from before the field existed, or a metadata source that does
  // not supply it, must not render "0 works" and libel a real author.
  it('shows nothing when the work count is absent', () => {
    renderResult();

    expect(screen.queryByText(/AuthorWorkCount/)).not.toBeInTheDocument();
    expect(screen.queryByText('AuthorNoWorks')).not.toBeInTheDocument();
  });

  it('shows the top work when there is one', () => {
    renderResult({ workCount: 355, topWork: 'The Hobbit' });

    expect(screen.getByText('AuthorTopWork(title=The Hobbit)')).toBeInTheDocument();
  });

  it('omits the top work when there is none', () => {
    renderResult({ workCount: 355 });

    expect(screen.queryByText(/AuthorTopWork/)).not.toBeInTheDocument();
  });

  // foreignAuthorId has been an OL key since the metadata cutover, but this
  // link still pointed at goodreads.com/author/show/{id} -- a 404 on every
  // single result, on the one control a user would click to check a match.
  it('links out to the Open Library author page', () => {
    renderResult({ workCount: 355 });

    const link = screen.getByRole('link', { name: '' });

    expect(link).toHaveAttribute('href', 'https://openlibrary.org/authors/OL26320A');
  });
});
