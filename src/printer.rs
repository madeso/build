pub struct Printer
{
    error_count: i32,
    errors: Vec<String>
}


impl Printer
{
    pub fn new() -> Printer
    {
        Printer
        {
            error_count: 0,
            errors: Vec::<String>::new()
        }
    }

    pub fn header(&self, text: &str)
    {
        println!("");
        println!("############################################");
        println!("##  {}", text);
        println!("############################################");
    }

    pub fn info(&self, text: &str)
    {
        println!("{}", text);
    }
    
    pub fn error(&mut self, text: &str)
    {
        self.error_count += 1;
        self.errors.push(String::from(text));
        println!("ERROR: {}", text);
    }

    pub fn exit_with_code(&self)
    {
        if self.error_count > 0
        {
            println!("Errors detected: ({})", self.error_count)
        }

        for error in &self.errors
        {
            println!("{}", error);
        }
    }
}

