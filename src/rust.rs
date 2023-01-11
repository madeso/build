// wtf rust... why isn't this included by default?
// from https://users.rust-lang.org/t/how-to-find-a-substring-starting-at-a-specific-index/8299
pub fn find_start_at(slice: &str, at: usize, pat: char) -> Option<usize>
{
    slice[at..].find(pat).map(|i| at + i)
}

pub fn find_start_at_str(slice: &str, at: usize, pat: &str) -> Option<usize>
{
    slice[at..].find(pat).map(|i| at + i)
}


// rust... why do you let me write this code
// why can't you just ship a number formatting that respects locale...


