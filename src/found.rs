use std::fmt;

#[derive(Debug)]
pub struct Found
{
    value: Option<String>,
    name: String
}

impl Found
{
    pub fn new(value: Option<String>, name: String) -> Found
    {
        Found{value, name}
    }
}

impl fmt::Display for Found
{
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result
    {
        match &self.value
        {
            Some(path) => write!(f, "Found {} from {}", path, self.name),
            None => write!(f, "NOT FOUND in {}", self.name)
        }
    }
}
