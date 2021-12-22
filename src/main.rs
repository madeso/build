mod printer;

fn main() {
    let print = printer::Printer::new();
    print.header("main");
    print.info("hello world");
    print.exit_with_code()
}
