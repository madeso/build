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
pub fn num_format(num: usize) -> String
{
    // println!("Formatting {}", num);

    let str = format!("{}", num);

    // println!("  str {}", str);
    let mut vec : Vec<char> = str.chars().collect();
    vec.reverse();
    // println!("  vec {:?}", vec);
    let mut new  = Vec::new();
    let mut index = 0;
    for c in vec
    {
        if index == 3
        {
            index = 1;
            new.push(' ');
            // println!("  with space");
        }
        else
        {
            index += 1;
        }
        new.push(c);
    }
    new.reverse();
    // println!("  new {:?}", new);

    let mut r = String::new();
    for c in new
    {
        r.push(c);
    }

    // println!("  ret {}", r);

    r
}

