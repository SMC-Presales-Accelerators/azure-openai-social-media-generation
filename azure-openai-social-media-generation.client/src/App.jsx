import './App.css';
import SocialMediaCreator from './social-media/SocialMediaCreator'
import Container from 'react-bootstrap/Container';
import Navbar from 'react-bootstrap/Navbar';
import Row from 'react-bootstrap/Row';
import Col from 'react-bootstrap/Col';
function App() {
    return (
        <>
            <Navbar bg="dark" data-bs-theme="dark">
                <Navbar.Brand className="me-auto m-1">Social Media Generator</Navbar.Brand>
                <Navbar.Brand href="https://github.com/cbattlegear/azure-openai-social-media-generation" className="mx-auto">Github</Navbar.Brand>
                <Navbar.Text className="ms-auto m-1">Azure OpenAI</Navbar.Text>
            </Navbar>
            <Container>
                <Row>
                    <Col>
                        <h2>Generate social media posts with Azure OpenAI!</h2>
                        <p>Upload the picture you would like featured and the marketing copy you would like the post based on.</p>
                        <p>Using Azure Vision AI and Azure OpenAI it will remove the background from your photo, generate a new background
                        following the theme of the post, and provide you a prewritten post.</p>
                    </Col>
                </Row>
                <SocialMediaCreator />
            </Container>
        </>
    );
}

export default App;