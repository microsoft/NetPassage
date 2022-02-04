const express = require("express");
const cors = require("cors");

const app = express();
app.use(cors());

const port = process.env.PORT || 3978;

app.get('/hello', (req, res) => {
    //Prints all the headers and its value as JavaScript object.
	console.log(req.headers);
    var body = 'hello world';
    // res.header('Content-Length', 50)
    // res.header('Accept-Ranges', 'bytes');
    // res.header('Transfer-Encoding', 'chunked')
    res.send({ message: body});
});

app.post('/test', (req, res) => {
    //Prints all the headers and its value as JavaScript object.
	console.log(req.headers);
    var body = 'hello world';
    res.header('Content-Length', 50)
    res.send({ message: body});
});

app.listen(port, () => {
    console.log(`Express app is listening at http://localhost:${port}`);
}
);